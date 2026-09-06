using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Domain.Notes;
using Davish.Result;
using NSubstitute;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Application.Notes.Commands;

public class CreateNoteCommandHandlerTests
{
    [Fact]
    public async Task GivenAValidCommand_WhenHandling_ThenCreatesAndPersistsANoteOwnedByTheCurrentUser()
    {
        var ownerAppUserId = Guid.NewGuid();
        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(ownerAppUserId);

        var noteRepository = Substitute.For<INoteRepository>();
        noteRepository
            .AddAsync(Arg.Any<NoteAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var handler = new CreateNoteCommandHandler(userContext, noteRepository, TimeProvider.System);
        var command = new CreateNoteCommand("Title", "Content");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.NoteId);
        await noteRepository.Received(1).AddAsync(
            Arg.Is<NoteAggregate>(n => n.OwnerAppUserId == ownerAppUserId && n.Title == "Title" && n.Content == "Content"),
            Arg.Any<CancellationToken>());
    }
}
