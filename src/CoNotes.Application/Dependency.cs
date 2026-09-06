using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Application.Decorators;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Delete;
using CoNotes.Application.Notes.Commands.Update;
using CoNotes.Application.Notes.Queries.Get;
using CoNotes.Application.Notes.Queries.GetGraph;
using CoNotes.Application.Notes.Queries.List;
using CoNotes.Application.Notes.Queries.Search;
using Microsoft.Extensions.DependencyInjection;

namespace CoNotes.Application;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSendr();
            services.AddSendrNotification();

            services
                .AddRequestHandler<UpsertAppUserCommand, Result<UpsertAppUserDto>, UpsertAppUserCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                .AddRequestHandler<CreateNoteCommand, Result<CreateNoteDto>, CreateNoteCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                .AddRequestHandler<UpdateNoteCommand, Result<UpdateNoteDto>, UpdateNoteCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                .AddRequestHandler<DeleteNoteCommand, Result, DeleteNoteCommandHandler>(o => o.Decorator.With<TransactionalDecorator>())
                .AddRequestHandler<ListNotesQuery, Result<ListNotesDto>, ListNotesQueryHandler>()
                .AddRequestHandler<GetNoteQuery, Result<GetNoteDto>, GetNoteQueryHandler>()
                .AddRequestHandler<SearchNotesByTitleQuery, Result<SearchNotesByTitleDto>, SearchNotesByTitleQueryHandler>()
                .AddRequestHandler<GetNoteGraphQuery, Result<NoteGraphDto>, GetNoteGraphQueryHandler>();

            return services;
        }
    }
}
