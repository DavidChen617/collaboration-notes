using CoNotes.Application.ChatMessages.Queries.GetHistory;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Join;
using CoNotes.Application.Notes.Commands.Share;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.ChatMessages;
using Davish.Sendr;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class ChatMessageTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenChatMessageSaved_WhenQueriedByNoteId_ThenReturnsMessageInOrder()
    {
        using var scope = factory.Services.CreateScope();
        var (owner, noteId) = await CreateOwnerAndNoteAsync(scope.ServiceProvider);
        var repository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
        var first = ChatMessage.Create(noteId, owner.Id, "First", DateTime.UtcNow);
        var second = ChatMessage.Create(noteId, owner.Id, "Second", DateTime.UtcNow.AddMilliseconds(1));

        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(second, CancellationToken.None);

        var messages = await repository.GetRecentByNoteIdAsync(noteId, 20, CancellationToken.None);

        Assert.Equal([first.Id, second.Id], messages.Select(message => message.Id));
        Assert.All(messages, message => Assert.Empty(message.DomainEvents));
    }

    [Fact]
    public async Task GivenOwnerCollaboratorAndUnrelatedUser_WhenGettingChatHistory_ThenAccessMatchesNote()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var appUserRepository = services.GetRequiredService<IAppUserRepository>();
        var userContext = services.GetRequiredService<TestUserContext>();
        var sender = services.GetRequiredService<ISender>();
        var chatMessageRepository = services.GetRequiredService<IChatMessageRepository>();
        var (owner, noteId) = await CreateOwnerAndNoteAsync(services);
        var collaborator = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var unrelatedUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(collaborator, CancellationToken.None);
        await appUserRepository.AddAsync(unrelatedUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var shareResult = await sender.SendAsync(new GenerateShareLinkCommand(noteId), CancellationToken.None);
        userContext.AppUserId = collaborator.Id;
        await sender.SendAsync(
            new JoinNoteViaShareLinkCommand(shareResult.Value.ShareToken),
            CancellationToken.None
        );
        await chatMessageRepository.AddAsync(
            ChatMessage.Create(noteId, owner.Id, "Shared history", DateTime.UtcNow),
            CancellationToken.None
        );

        userContext.AppUserId = owner.Id;
        var ownerResult = await sender.SendAsync(new GetChatHistoryQuery(noteId), CancellationToken.None);
        Assert.True(ownerResult.IsSuccess);
        Assert.Equal("Shared history", Assert.Single(ownerResult.Value.Messages).Content);

        userContext.AppUserId = collaborator.Id;
        var collaboratorResult = await sender.SendAsync(new GetChatHistoryQuery(noteId), CancellationToken.None);
        Assert.True(collaboratorResult.IsSuccess);
        Assert.Equal("Shared history", Assert.Single(collaboratorResult.Value.Messages).Content);

        userContext.AppUserId = unrelatedUser.Id;
        var unrelatedResult = await sender.SendAsync(new GetChatHistoryQuery(noteId), CancellationToken.None);
        Assert.False(unrelatedResult.IsSuccess);
        Assert.Equal("ChatMessage.GetHistory", unrelatedResult.Error.Code);
    }

    private static async Task<(AppUser Owner, Guid NoteId)> CreateOwnerAndNoteAsync(
        IServiceProvider services
    )
    {
        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var appUserRepository = services.GetRequiredService<IAppUserRepository>();
        var userContext = services.GetRequiredService<TestUserContext>();
        var sender = services.GetRequiredService<ISender>();

        await appUserRepository.AddAsync(owner, CancellationToken.None);
        userContext.AppUserId = owner.Id;
        var createResult = await sender.SendAsync(
            new CreateNoteCommand("Chat note", "Note content"),
            CancellationToken.None
        );

        return (owner, createResult.Value.NoteId);
    }
}
