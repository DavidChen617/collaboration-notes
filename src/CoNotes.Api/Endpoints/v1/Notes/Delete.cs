using CoNotes.Application.Notes.Commands.Delete;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class DeleteNoteEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/{noteId}", HandleAsync)
            .WithName("DeleteNote")
            .WithSummary("刪除筆記")
            .WithDescription("刪除筆記, 如果使用者不是擁有者則會刪除失敗")
            .ProducesProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProducesProblem(StatusCodes.Status400BadRequest, "用戶沒有刪除該筆記的權利!");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DeleteNoteCommand(noteId), ct);

        return result.ToNoContent();
    }
}
