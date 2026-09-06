using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.Update;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class UpdateNoteEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/{noteId}", HandleAsync)
            .WithName("UpdateNote")
            .WithSummary("更新筆記")
            .WithDescription("更新筆記標題與內容, 如果使用者不是擁有者則會更新失敗")
            .Produces<UpdateNoteDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限更新這篇筆記, 或內容中連結的筆記不屬於自己");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid noteId,
        UpdateNoteRequest request,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new UpdateNoteCommand(noteId, request.Title, request.Content), ct);

        return result.ToOk();
    }
}

public sealed record UpdateNoteRequest(string Title, string Content);
