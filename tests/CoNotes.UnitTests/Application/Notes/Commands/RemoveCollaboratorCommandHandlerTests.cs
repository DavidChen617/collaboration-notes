using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.RemoveCollaborator;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class RemoveCollaboratorCommandHandlerTests
{
    [Fact]
    public async Task GivenOwner_WhenRemovingCollaborator_ThenRemovedAndCannotAccessAfterward()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new RemoveCollaboratorCommandHandler(userContext, noteRepository);
        var command = new RemoveCollaboratorCommand(note.Id, collaboratorAppUserId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(note.IsAccessibleBy(collaboratorAppUserId));
        await noteRepository.Received(1).UpdateAsync(note, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonOwner_WhenRemovingCollaborator_ThenIsRejectedAndDoesNotPersist()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(collaboratorAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new RemoveCollaboratorCommandHandler(userContext, noteRepository);
        var command = new RemoveCollaboratorCommand(note.Id, collaboratorAppUserId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.RemoveCollaborator", result.Error.Code);
        await noteRepository.DidNotReceive().UpdateAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }
}
