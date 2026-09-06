using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.Join;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class JoinNoteViaShareLinkEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/share-link/{shareToken}/join", HandleAsync)
            .WithName("JoinNoteViaShareLink")
            .WithSummary("透過分享連結加入共編")
            .WithDescription("已登入使用者開啟有效的分享連結時, 加入這篇筆記的共編者名單")
            .Produces<JoinNoteViaShareLinkDto>()
            .ProduceProblem(StatusCodes.Status400BadRequest, "分享連結無效或已失效");
    }

    private static async Task<IResult> HandleAsync([FromRoute] string shareToken, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new JoinNoteViaShareLinkCommand(shareToken), ct);

        return result.ToOk();
    }
}
