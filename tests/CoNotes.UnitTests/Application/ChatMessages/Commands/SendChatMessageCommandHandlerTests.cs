using CoNotes.Application.Abstractions;
using CoNotes.Application.ChatMessages.Commands.Send;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.ChatMessages;
using CoNotes.Domain.Notes;
using Davish.Result;
using NSubstitute;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.ChatMessages.Commands;

public class SendChatMessageCommandHandlerTests
{
    [Fact]
    public async Task GivenOwner_WhenSendingChatMessage_ThenMessageIsStoredAndBroadcast()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var dependencies = CreateDependencies(note, ownerAppUserId);
        var handler = dependencies.CreateHandler();

        var result = await handler.HandleAsync(
            new SendChatMessageCommand(note.Id, "Hello"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ownerAppUserId, result.Value.AuthorAppUserId);
        await dependencies.ChatMessageRepository.Received(1).AddAsync(
            Arg.Is<ChatMessage>(message =>
                message.NoteId == note.Id &&
                message.AuthorAppUserId == ownerAppUserId &&
                message.Content == "Hello"
            ),
            Arg.Any<CancellationToken>()
        );
        await dependencies.Broadcaster.Received(1).BroadcastAsync(
            Arg.Is<ChatMessage>(message => message.Id == result.Value.ChatMessageId),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenCollaborator_WhenSendingChatMessage_ThenMessageIsStoredAndBroadcast()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);
        var dependencies = CreateDependencies(note, collaboratorAppUserId);
        var handler = dependencies.CreateHandler();

        var result = await handler.HandleAsync(
            new SendChatMessageCommand(note.Id, "Collaborator message"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await dependencies.ChatMessageRepository.Received(1).AddAsync(
            Arg.Is<ChatMessage>(message => message.AuthorAppUserId == collaboratorAppUserId),
            Arg.Any<CancellationToken>()
        );
        await dependencies.Broadcaster.Received(1).BroadcastAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenUnrelatedUser_WhenSendingChatMessage_ThenRejectedWithoutPersistenceOrBroadcast()
    {
        var note = NoteAggregate.Create(Guid.NewGuid(), "Title", "Content", DateTime.UtcNow);
        var dependencies = CreateDependencies(note, Guid.NewGuid());
        var handler = dependencies.CreateHandler();

        var result = await handler.HandleAsync(
            new SendChatMessageCommand(note.Id, "Unauthorized"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChatMessage.Send", result.Error.Code);
        await dependencies.ChatMessageRepository.DidNotReceive().AddAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
        await dependencies.Broadcaster.DidNotReceive().BroadcastAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenOwnerPlanTierProMax_WhenSendChatMessageCommandHandled_ThenSucceeds()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var dependencies = CreateDependencies(note, ownerAppUserId, PlanTier.ProMax);
        var handler = dependencies.CreateHandler();

        var result = await handler.HandleAsync(
            new SendChatMessageCommand(note.Id, "Hello"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(PlanTier.Free)]
    [InlineData(PlanTier.Pro)]
    public async Task GivenOwnerPlanTierNotProMax_WhenSendChatMessageCommandHandled_ThenRejected(PlanTier planTier)
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var dependencies = CreateDependencies(note, ownerAppUserId, planTier);
        var handler = dependencies.CreateHandler();

        var result = await handler.HandleAsync(
            new SendChatMessageCommand(note.Id, "Hello"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChatMessage.Send", result.Error.Code);
        await dependencies.ChatMessageRepository.DidNotReceive().AddAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
    }

    private static Dependencies CreateDependencies(
        NoteAggregate note,
        Guid requestingAppUserId,
        PlanTier ownerPlanTier = PlanTier.ProMax
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(requestingAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var owner = AppUserAggregate.Rehydrate(note.OwnerAppUserId, Guid.NewGuid().ToString(), DateTime.UtcNow, ownerPlanTier);
        var appUserRepository = Substitute.For<IAppUserRepository>();
        appUserRepository.FindByIdAsync(note.OwnerAppUserId, Arg.Any<CancellationToken>()).Returns(owner);

        var chatMessageRepository = Substitute.For<IChatMessageRepository>();
        chatMessageRepository
            .AddAsync(Arg.Any<ChatMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return new Dependencies(
            userContext,
            noteRepository,
            appUserRepository,
            chatMessageRepository,
            Substitute.For<IChatMessageBroadcaster>()
        );
    }

    private sealed record Dependencies(
        IUserContext UserContext,
        INoteRepository NoteRepository,
        IAppUserRepository AppUserRepository,
        IChatMessageRepository ChatMessageRepository,
        IChatMessageBroadcaster Broadcaster
    )
    {
        public SendChatMessageCommandHandler CreateHandler() =>
            new(UserContext, NoteRepository, AppUserRepository, ChatMessageRepository, Broadcaster, TimeProvider.System);
    }
}
