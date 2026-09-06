using CoNotes.Application.Notes.Queries.GetGraph;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class GetNoteGraphEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/graph", HandleAsync)
            .WithName("GetNoteGraph")
            .WithSummary("取得筆記關係圖")
            .WithDescription("取得目前使用者所有筆記（節點）與筆記連結（邊）組成的關係圖資料")
            .Produces<NoteGraphDto>();
    }

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new GetNoteGraphQuery(), ct);

        return result.ToOk();
    }
}
