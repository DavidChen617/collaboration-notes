using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Delete;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class DeleteNoteCommandHandlerTests
{
    [Fact]
    public async Task GivenOwnerDeletesTheirOwnNote_WhenHandling_ThenDeletesIt()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new DeleteNoteCommandHandler(userContext, noteRepository);
        var command = new DeleteNoteCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await noteRepository.Received(1).DeleteAsync(note, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonOwnerDeletesSomeoneElsesNote_WhenHandling_ThenIsRejectedAndDoesNotDelete()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(otherAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new DeleteNoteCommandHandler(userContext, noteRepository);
        var command = new DeleteNoteCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Delete", result.Error.Code);
        await noteRepository.DidNotReceive().DeleteAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNoteDoesNotExist_WhenHandling_ThenReturnsNotFound()
    {
        var userContext = Substitute.For<IUserContext>();
        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((NoteAggregate?)null);

        var handler = new DeleteNoteCommandHandler(userContext, noteRepository);
        var command = new DeleteNoteCommand(Guid.NewGuid());

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Delete", result.Error.Code);
    }
}
