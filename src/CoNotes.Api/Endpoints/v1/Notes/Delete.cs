using CoNotes.Application.Notes.Commands.Delete;
using Microsoft.AspNetCore.Mvc;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class DeleteNoteEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/{noteId}", HandleAsync)
            .WithName("DeleteNote")
            .WithSummary("刪除筆記")
            .WithDescription("刪除筆記, 如果使用者不是擁有者則會刪除失敗");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DeleteNoteCommand(noteId), ct);

        return result.ToNoContent();
    }
}
