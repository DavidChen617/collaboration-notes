using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.Share;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GenerateShareLinkEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/{noteId}/share-link", HandleAsync)
            .WithName("GenerateShareLink")
            .WithSummary("產生分享連結")
            .WithDescription("為這篇筆記產生一條新的分享連結, 取代目前(若有)的連結, 如果使用者不是擁有者則會失敗")
            .Produces<GenerateShareLinkDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "筆記找不到")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有權限產生這篇筆記的分享連結");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new GenerateShareLinkCommand(noteId), ct);

        return result.ToOk();
    }
}
