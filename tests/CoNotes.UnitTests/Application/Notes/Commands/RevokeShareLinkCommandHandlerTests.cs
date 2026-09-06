using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Revoke;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class RevokeShareLinkCommandHandlerTests
{
    [Fact]
    public async Task GivenActiveShareLink_WhenRevoking_ThenNewLinkGeneratedAndCollaboratorsUnaffected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var oldToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(oldToken, collaboratorAppUserId);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new RevokeShareLinkCommandHandler(userContext, noteRepository);
        var command = new RevokeShareLinkCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldToken, result.Value.ShareToken);
        Assert.Equal(note.ShareToken, result.Value.ShareToken);
        Assert.Contains(collaboratorAppUserId, note.CollaboratorAppUserIds);
        await noteRepository.Received(1).UpdateAsync(note, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonOwner_WhenRevokingShareLink_ThenIsRejectedAndDoesNotPersist()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        note.GenerateShareLink(ownerAppUserId);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(otherAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new RevokeShareLinkCommandHandler(userContext, noteRepository);
        var command = new RevokeShareLinkCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.RevokeShareLink", result.Error.Code);
        await noteRepository.DidNotReceive().UpdateAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }
}
