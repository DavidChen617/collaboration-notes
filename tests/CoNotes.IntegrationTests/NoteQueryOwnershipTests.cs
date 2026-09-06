using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Queries.Get;
using CoNotes.Application.Notes.Queries.List;
using CoNotes.Domain.AppUsers;
using Davish.Sendr;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class NoteQueryOwnershipTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task ListNotesQuery_only_returns_the_calling_users_own_notes()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        var otherUser = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(otherUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        await sender.SendAsync(new CreateNoteCommand("Owner's note", "Content"), CancellationToken.None);

        userContext.AppUserId = otherUser.Id;
        await sender.SendAsync(new CreateNoteCommand("Other user's note", "Content"), CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var result = await sender.SendAsync(new ListNotesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Notes);
        Assert.Equal("Owner's note", result.Value.Notes[0].Title);
    }

    [Fact]
    public async Task GetNoteQuery_succeeds_for_the_owner_and_is_rejected_for_a_non_owner()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        var otherUser = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(otherUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var createResult = await sender.SendAsync(new CreateNoteCommand("Title", "Content"), CancellationToken.None);
        var noteId = createResult.Value.NoteId;

        var ownerResult = await sender.SendAsync(new GetNoteQuery(noteId), CancellationToken.None);
        Assert.True(ownerResult.IsSuccess);
        Assert.Equal("Title", ownerResult.Value.Title);

        userContext.AppUserId = otherUser.Id;
        var nonOwnerResult = await sender.SendAsync(new GetNoteQuery(noteId), CancellationToken.None);
        Assert.False(nonOwnerResult.IsSuccess);
        Assert.Equal("Note.Get", nonOwnerResult.Error.Code);
    }
}
