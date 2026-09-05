using CoNotes.Application.Notes.Commands.Update;
using Microsoft.AspNetCore.Mvc;

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
            .Produces<UpdateNoteDto>();
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
