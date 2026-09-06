using CoNotes.Api.Extensions;
using CoNotes.Application.Notes.Commands.Create;

namespace CoNotes.Api.Endpoints.v1.Notes;

internal sealed class CreateNoteEndpoint : IEndpoint<NoteGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/", HandleAsync)
            .WithName("CreateNote")
            .WithSummary("建立筆記")
            .WithDescription("建立一筆屬於目前使用者的新筆記")
            .Produces<CreateNoteDto>(StatusCodes.Status201Created)
            .ProduceProblem(StatusCodes.Status400BadRequest, "內容中連結的筆記不屬於自己");
    }

    private static async Task<IResult> HandleAsync(CreateNoteRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CreateNoteCommand(request.Title, request.Content), ct);

        return result.ToCreated("GetNote", v => new { noteId = v.NoteId });
    }
}

public sealed record CreateNoteRequest(string Title, string Content);
