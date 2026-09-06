using CoNotes.Api.Extensions;
using CoNotes.Application.ChatMessages.Commands.Send;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class SendChatMessageEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/{noteId}/chat/messages", HandleAsync)
            .WithName("SendChatMessage")
            .WithSummary("傳送聊天訊息")
            .WithDescription("在目前使用者有權限存取的筆記聊天室傳送訊息")
            .Produces<SendChatMessageDto>()
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限存取這篇筆記的聊天室")
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid noteId,
        SendChatMessageRequest request,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(
            new SendChatMessageCommand(noteId, request.Content),
            ct
        );

        return result.ToOk();
    }
}

public sealed record SendChatMessageRequest(string Content);
