using CoNotes.Application.Abstractions;
using CoNotes.Domain.Notes;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace CoNotes.Application.Notes.Commands.Create;

internal sealed class CreateNoteCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<CreateNoteCommand, Result<CreateNoteDto>>
{
    public async Task<Result<CreateNoteDto>> HandleAsync(
        CreateNoteCommand command,
        CancellationToken cancellationToken
    )
    {
        var ownerAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        var note = NoteAggregate.Create(ownerAppUserId, command.Title, command.Content);

        var requestedTargetNoteIds = NoteLinkContentParser.ExtractLinkedNoteIds(command.Content);
        var ownedTargetNoteIds = await noteRepository.FindOwnedNoteIdsAsync(ownerAppUserId, requestedTargetNoteIds, cancellationToken);

        var resolveLinksResult = note.ResolveLinks(requestedTargetNoteIds, ownedTargetNoteIds);
        if (!resolveLinksResult.IsSuccess)
            return resolveLinksResult.Error;

        await noteRepository.AddAsync(note, cancellationToken);

        return new CreateNoteDto(note.Id);
    }
}
