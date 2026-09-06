using CoNotes.Api.Extensions;
using CoNotes.Application.ChatMessages.Queries.GetHistory;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GetChatHistoryEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/{noteId}/chat/messages", HandleAsync)
            .WithName("GetChatHistory")
            .WithSummary("取得聊天歷史")
            .WithDescription("取得目前使用者有權限存取的筆記聊天室訊息")
            .Produces<GetChatHistoryDto>()
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限存取這篇筆記的聊天室");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid noteId,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new GetChatHistoryQuery(noteId), ct);

        return result.ToOk();
    }
}
