using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.Revoke;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class RevokeShareLinkEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/{noteId}/share-link/revoke", HandleAsync)
            .WithName("RevokeShareLink")
            .WithSummary("撤銷分享連結")
            .WithDescription("撤銷這篇筆記目前的分享連結(舊連結立即失效)並產生新的連結, 不影響既有共編者, 如果使用者不是擁有者則會失敗")
            .Produces<RevokeShareLinkDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限撤銷這篇筆記的分享連結");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new RevokeShareLinkCommand(noteId), ct);

        return result.ToOk();
    }
}
