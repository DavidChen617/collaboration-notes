using CoNotes.Application.Notes.Queries.List;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class ListNotesEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/", HandleAsync)
            .WithName("ListNotes")
            .WithSummary("列出筆記")
            .WithDescription("列出目前使用者擁有的所有筆記")
            .Produces<ListNotesDto>();
    }

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ListNotesQuery(), ct);

        return result.ToOk();
    }
}
