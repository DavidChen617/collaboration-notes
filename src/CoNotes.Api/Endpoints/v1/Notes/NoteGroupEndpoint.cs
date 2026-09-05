namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class NoteGroupEndpoint : IGroupEndpoint<ApiVersionEndpoint>
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
    {
        var notes = endpoints
                    .MapGroup("notes")
                    .WithTags("notes")
                    .HasApiVersion(1.0)
                    .RequireAuthorization();

        return notes;
    }
}
