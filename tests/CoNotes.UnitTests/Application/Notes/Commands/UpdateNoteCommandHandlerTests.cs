using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Update;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class UpdateNoteCommandHandlerTests
{
    [Fact]
    public async Task GivenOwnerUpdatesTheirOwnNote_WhenHandling_ThenAppliesTheUpdateAndReturnsIt()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Old title", "Old content", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new UpdateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new UpdateNoteCommand(note.Id, "New title", "New content");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", result.Value.Title);
        Assert.Equal("New content", result.Value.Content);
        await noteRepository.Received(1).UpdateAsync(note, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonOwnerUpdatesSomeoneElsesNote_WhenHandling_ThenIsRejectedAndDoesNotPersist()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Old title", "Old content", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(otherAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new UpdateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new UpdateNoteCommand(note.Id, "New title", "New content");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Update", result.Error.Code);
        await noteRepository.DidNotReceive().UpdateAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenValidLinks_WhenSaveNoteCommandHandled_ThenNoteLinkRepositoryCalledWithReplacementSet()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Old title", "Old content", DateTime.UtcNow);
        var targetNoteId = Guid.NewGuid();
        var newContent = $"""<p>See <span data-note-link="{targetNoteId}">Other note</span></p>""";

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);
        noteRepository
            .FindOwnedNoteIdsAsync(ownerAppUserId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(targetNoteId)), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { targetNoteId });

        var handler = new UpdateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new UpdateNoteCommand(note.Id, "New title", newContent);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([targetNoteId], note.LinkedNoteIds);
        await noteRepository.Received(1).UpdateAsync(
            Arg.Is<NoteAggregate>(n => n.LinkedNoteIds.SequenceEqual(new[] { targetNoteId })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenLinkToNoteNotOwnedByCaller_WhenHandling_ThenIsRejectedAndDoesNotPersist()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Old title", "Old content", DateTime.UtcNow);
        var otherUsersNoteId = Guid.NewGuid();
        var newContent = $"""<p>See <span data-note-link="{otherUsersNoteId}">Other note</span></p>""";

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);
        noteRepository
            .FindOwnedNoteIdsAsync(ownerAppUserId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        var handler = new UpdateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new UpdateNoteCommand(note.Id, "New title", newContent);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.ResolveLinks", result.Error.Code);
        await noteRepository.DidNotReceive().UpdateAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNoteDoesNotExist_WhenHandling_ThenReturnsNotFound()
    {
        var userContext = Substitute.For<IUserContext>();
        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((NoteAggregate?)null);

        var handler = new UpdateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new UpdateNoteCommand(Guid.NewGuid(), "Title", "Content");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Update", result.Error.Code);
    }
}
