using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Delete;
using CoNotes.Application.Notes.Commands.Update;
using CoNotes.Application.Notes.Queries.Get;
using CoNotes.Application.Notes.Queries.List;
using Microsoft.Extensions.DependencyInjection;

namespace CoNotes.Application;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSendr();

            services
                .AddRequestHandler<UpsertAppUserCommand, Result<UpsertAppUserDto>, UpsertAppUserCommandHandler>()
                .AddRequestHandler<CreateNoteCommand, Result<CreateNoteDto>, CreateNoteCommandHandler>()
                .AddRequestHandler<UpdateNoteCommand, Result<UpdateNoteDto>, UpdateNoteCommandHandler>()
                .AddRequestHandler<DeleteNoteCommand, Result, DeleteNoteCommandHandler>()
                .AddRequestHandler<ListNotesQuery, Result<ListNotesDto>, ListNotesQueryHandler>()
                .AddRequestHandler<GetNoteQuery, Result<GetNoteDto>, GetNoteQueryHandler>();

            return services;
        }
    }
}
