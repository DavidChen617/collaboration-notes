using CoNotes.Application.Abstractions;
using CoNotes.Application.ChatMessages.Commands.GenerateAiReply;
using CoNotes.Domain.ChatMessages;
using CoNotes.Domain.ChatMessages.Events;
using CoNotes.Domain.Notes;
using Davish.Result;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.ChatMessages.Commands;

public class GenerateAiReplyCommandHandlerTests
{
    [Fact]
    public async Task GivenPrimaryProviderUnavailable_WhenGeneratingAiReply_ThenFallbackProviderIsUsed()
    {
        var dependencies = CreateDependencies("Note content");
        var primary = CreateProvider("primary", reply: null);
        var fallback = CreateProvider("fallback", new AiChatReply("Fallback reply"));
        var handler = dependencies.CreateHandler([primary, fallback]);

        var result = await handler.HandleAsync(dependencies.Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fallback", result.Value.ProviderUsed);
        Assert.Equal("Fallback reply", result.Value.Content);
        await primary.Received(1).TryGetReplyAsync(Arg.Any<AiChatContext>(), Arg.Any<CancellationToken>());
        await fallback.Received(1).TryGetReplyAsync(Arg.Any<AiChatContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenAllProvidersUnavailable_WhenGeneratingAiReply_ThenAiReplyFailedRaisedAndFallbackMessageStored()
    {
        var dependencies = CreateDependencies("Note content");
        var handler = dependencies.CreateHandler([
            CreateProvider("primary", reply: null),
            CreateProvider("fallback", reply: null),
        ]);

        var result = await handler.HandleAsync(dependencies.Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ProviderUsed);
        Assert.Equal(GenerateAiReplyCommandHandler.FailureMessage, result.Value.Content);
        await dependencies.ChatMessageRepository.Received(1).AddAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
        var savedMessage = GetSavedMessage(dependencies.ChatMessageRepository);
        Assert.True(savedMessage.IsAiReply);
        Assert.Null(savedMessage.AuthorAppUserId);
        var failedEvent = Assert.IsType<AiReplyFailedDomainEvent>(Assert.Single(savedMessage.DomainEvents));
        Assert.Equal(dependencies.Command.TriggeringChatMessageId, failedEvent.TriggeringChatMessageId);
    }

    [Fact]
    public async Task GivenAnyProviderSucceeds_WhenGeneratingAiReply_ThenAiReplyGeneratedRaisedWithReplyContent()
    {
        var dependencies = CreateDependencies("Note content");
        var provider = CreateProvider("provider-a", new AiChatReply("Generated reply"));
        var handler = dependencies.CreateHandler([provider]);

        var result = await handler.HandleAsync(dependencies.Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await dependencies.ChatMessageRepository.Received(1).AddAsync(
            Arg.Any<ChatMessage>(),
            Arg.Any<CancellationToken>()
        );
        var savedMessage = GetSavedMessage(dependencies.ChatMessageRepository);
        Assert.Equal("Generated reply", savedMessage.Content);
        var generatedEvent = Assert.IsType<AiReplyGeneratedDomainEvent>(Assert.Single(savedMessage.DomainEvents));
        Assert.Equal("provider-a", generatedEvent.ProviderUsed);
        await dependencies.Broadcaster.Received(1).BroadcastAsync(
            Arg.Is<ChatMessage>(message => message.Id == result.Value.ChatMessageId),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenNoteContentExceedsLengthLimit_WhenBuildingAiContext_ThenContentIsTruncated()
    {
        var noteContent = new string('x', GenerateAiReplyCommandHandler.MaxNoteContentLength + 50);
        var dependencies = CreateDependencies(noteContent);
        AiChatContext? capturedContext = null;
        var provider = Substitute.For<IAiChatProvider>();
        provider.Name.Returns("provider");
        provider
            .TryGetReplyAsync(Arg.Any<AiChatContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<AiChatContext>();
                return new AiChatReply("Reply");
            });
        var handler = dependencies.CreateHandler([provider]);

        var result = await handler.HandleAsync(dependencies.Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContext);
        Assert.Equal(GenerateAiReplyCommandHandler.MaxNoteContentLength, capturedContext.NoteContent.Length);
        Assert.Equal(noteContent[..GenerateAiReplyCommandHandler.MaxNoteContentLength], capturedContext.NoteContent);
    }

    private static IAiChatProvider CreateProvider(string name, AiChatReply? reply)
    {
        var provider = Substitute.For<IAiChatProvider>();
        provider.Name.Returns(name);
        provider
            .TryGetReplyAsync(Arg.Any<AiChatContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(reply));
        return provider;
    }

    private static ChatMessage GetSavedMessage(IChatMessageRepository repository)
    {
        return repository
            .ReceivedCalls()
            .Select(call => call.GetArguments()[0])
            .OfType<ChatMessage>()
            .Single();
    }

    private static Dependencies CreateDependencies(string noteContent)
    {
        var note = NoteAggregate.Create(Guid.NewGuid(), "Title", noteContent, DateTime.UtcNow);
        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var chatMessageRepository = Substitute.For<IChatMessageRepository>();
        chatMessageRepository
            .GetRecentByNoteIdAsync(note.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ChatMessage>());
        chatMessageRepository
            .AddAsync(Arg.Any<ChatMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return new Dependencies(
            new GenerateAiReplyCommand(note.Id, Guid.NewGuid()),
            noteRepository,
            chatMessageRepository,
            Substitute.For<IChatMessageBroadcaster>()
        );
    }

    private sealed record Dependencies(
        GenerateAiReplyCommand Command,
        INoteRepository NoteRepository,
        IChatMessageRepository ChatMessageRepository,
        IChatMessageBroadcaster Broadcaster
    )
    {
        public GenerateAiReplyCommandHandler CreateHandler(IEnumerable<IAiChatProvider> providers) =>
            new(NoteRepository, ChatMessageRepository, providers, Broadcaster, TimeProvider.System);
    }
}
