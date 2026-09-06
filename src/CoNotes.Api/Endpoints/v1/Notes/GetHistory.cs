using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Queries.GetHistory;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GetNoteHistoryEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/{noteId}/history", HandleAsync)
            .WithName("GetNoteHistory")
            .WithSummary("取得筆記編輯歷史")
            .WithDescription("取得回放某個時間點(預設為現在)筆記內容所需要的快照與後續更新紀錄, 前端用 Yjs 自己的 API 疊加回放")
            .Produces<GetNoteHistoryDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限檢視這篇筆記的編輯歷史");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid noteId,
        [FromQuery] DateTime? at,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new GetNoteHistoryQuery(noteId, at ?? DateTime.UtcNow), ct);

        return result.ToOk();
    }
}
