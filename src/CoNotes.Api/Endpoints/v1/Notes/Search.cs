using CoNotes.Application.Notes.Queries.Search;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class SearchNotesByTitleEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/search", HandleAsync)
            .WithName("SearchNotesByTitle")
            .WithSummary("依標題搜尋筆記")
            .WithDescription("依標題關鍵字搜尋目前使用者擁有的筆記，供 [[ 連結自動完成使用")
            .Produces<SearchNotesByTitleDto>();
    }

    private static async Task<IResult> HandleAsync([FromQuery] string keyword, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new SearchNotesByTitleQuery(keyword), ct);

        return result.ToOk();
    }
}
