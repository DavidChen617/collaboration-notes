using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Queries.GetCollaboration;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GetNoteCollaborationEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/{noteId}/collaboration", HandleAsync)
            .WithName("GetNoteCollaboration")
            .WithSummary("取得筆記共編設定")
            .WithDescription("取得目前分享連結與共編者名單, 僅筆記擁有者可以存取")
            .Produces<GetNoteCollaborationDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限管理這篇筆記的共編設定");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new GetNoteCollaborationQuery(noteId), ct);

        return result.ToOk();
    }
}
