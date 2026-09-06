using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.RemoveCollaborator;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class RemoveCollaboratorEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/{noteId}/collaborators/{collaboratorAppUserId}", HandleAsync)
            .WithName("RemoveCollaborator")
            .WithSummary("移除共編者")
            .WithDescription("將指定的共編者從這篇筆記的共編者名單中移除, 如果使用者不是擁有者則會失敗")
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到, 或該使用者不是共編者")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限移除共編者");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid noteId,
        [FromRoute] Guid collaboratorAppUserId,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new RemoveCollaboratorCommand(noteId, collaboratorAppUserId), ct);

        return result.ToNoContent();
    }
}
