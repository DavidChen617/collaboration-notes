using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Share;
using CoNotes.Domain.Notes;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class GenerateShareLinkCommandHandlerTests
{
    [Fact]
    public async Task GivenOwner_WhenGeneratingShareLink_ThenReturnsUniqueUnguessableToken()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new GenerateShareLinkCommandHandler(userContext, noteRepository);
        var command = new GenerateShareLinkCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.ShareToken));
        Assert.Equal(note.ShareToken?.Value, result.Value.ShareToken);
        await noteRepository.Received(1).UpdateAsync(note, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonOwner_WhenGeneratingShareLink_ThenIsRejectedAndDoesNotPersist()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(otherAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository.GetByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);

        var handler = new GenerateShareLinkCommandHandler(userContext, noteRepository);
        var command = new GenerateShareLinkCommand(note.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.GenerateShareLink", result.Error.Code);
        await noteRepository.DidNotReceive().UpdateAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>());
    }
}
