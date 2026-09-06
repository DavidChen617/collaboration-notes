using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Join;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class JoinNoteViaShareLinkCommandHandlerTests
{
    [Fact]
    public async Task GivenDifferentUsers_WhenBothJoinWithSameShareLink_ThenBothAreAddedAsCollaborators()
    {
        var ownerAppUserId = Guid.NewGuid();
        var firstJoiningAppUserId = Guid.NewGuid();
        var secondJoiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(shareToken);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByShareTokenAsync(shareToken, Arg.Any<CancellationToken>()).Returns(note);

        var firstUserContext = Substitute.For<IUserContext>();
        firstUserContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(firstJoiningAppUserId);
        var firstHandler = new JoinNoteViaShareLinkCommandHandler(firstUserContext, noteRepository);

        var secondUserContext = Substitute.For<IUserContext>();
        secondUserContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(secondJoiningAppUserId);
        var secondHandler = new JoinNoteViaShareLinkCommandHandler(secondUserContext, noteRepository);

        var command = new JoinNoteViaShareLinkCommand(shareToken.Value);

        var firstResult = await firstHandler.HandleAsync(command, CancellationToken.None);
        var secondResult = await secondHandler.HandleAsync(command, CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Contains(firstJoiningAppUserId, note.CollaboratorAppUserIds);
        Assert.Contains(secondJoiningAppUserId, note.CollaboratorAppUserIds);
    }

    [Fact]
    public async Task GivenSameUser_WhenOpeningLinkTwice_ThenDoesNotProduceDuplicateRecord()
    {
        var ownerAppUserId = Guid.NewGuid();
        var joiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(shareToken);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(joiningAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByShareTokenAsync(shareToken, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new JoinNoteViaShareLinkCommandHandler(userContext, noteRepository);
        var command = new JoinNoteViaShareLinkCommand(shareToken.Value);

        await handler.HandleAsync(command, CancellationToken.None);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(note.CollaboratorAppUserIds);
    }

    [Fact]
    public async Task GivenInvalidOrRevokedShareToken_WhenJoining_ThenIsRejected()
    {
        var joiningAppUserId = Guid.NewGuid();
        var shareToken = Guid.NewGuid().ToString();

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(joiningAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByShareTokenAsync(Arg.Any<ShareLinkToken>(), Arg.Any<CancellationToken>()).Returns((NoteAggregate?)null);

        var handler = new JoinNoteViaShareLinkCommandHandler(userContext, noteRepository);
        var command = new JoinNoteViaShareLinkCommand(shareToken);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.JoinViaShareLink", result.Error.Code);
    }
}
