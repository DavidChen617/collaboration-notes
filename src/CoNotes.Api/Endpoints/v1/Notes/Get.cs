using CoNotes.Application.Notes.Queries.Get;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GetNoteEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/{noteId}", HandleAsync)
            .WithName("GetNote")
            .WithSummary("取得單筆筆記")
            .WithDescription("依 noteId 取得單筆筆記, 如果使用者不是擁有者則會取得失敗")
            .Produces<GetNoteDto>();
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid noteId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new GetNoteQuery(noteId), ct);

        return result.ToOk();
    }
}
