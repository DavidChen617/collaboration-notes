using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Update;
using CoNotes.Application.Notes.Queries.GetGraph;
using CoNotes.Application.Notes.Queries.Search;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Notes;
using Davish.Sendr;
using Microsoft.Extensions.DependencyInjection;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class NoteLinkTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenNoteWithLinks_WhenSaved_ThenNoteLinkTableMatchesContent()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var noteRepository = scope.ServiceProvider.GetRequiredService<INoteRepository>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);

        var target = NoteAggregate.Create(owner.Id, "Target", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(target, CancellationToken.None);

        var source = NoteAggregate.Create(owner.Id, "Source", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(source, CancellationToken.None);

        source.Update(owner.Id, "Source", $"""<p>See <span data-note-link="{target.Id}">Target</span></p>""", DateTime.UtcNow);
        source.ResolveLinks([target.Id], new HashSet<Guid> { target.Id });
        await noteRepository.UpdateAsync(source, CancellationToken.None);

        var reloaded = await noteRepository.GetByIdAsync(source.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal([target.Id], reloaded.LinkedNoteIds);
    }

    [Fact]
    public async Task GivenNoteDeleted_WhenQueried_ThenNoLinkRowsRemain()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var noteRepository = scope.ServiceProvider.GetRequiredService<INoteRepository>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);

        var target = NoteAggregate.Create(owner.Id, "Target", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(target, CancellationToken.None);

        var source = NoteAggregate.Create(owner.Id, "Source", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(source, CancellationToken.None);
        source.ResolveLinks([target.Id], new HashSet<Guid> { target.Id });
        await noteRepository.UpdateAsync(source, CancellationToken.None);

        target.Delete(owner.Id);
        await noteRepository.DeleteAsync(target, CancellationToken.None);

        var reloadedSource = await noteRepository.GetByIdAsync(source.Id, CancellationToken.None);

        Assert.NotNull(reloadedSource);
        Assert.Empty(reloadedSource.LinkedNoteIds);
    }

    [Fact]
    public async Task GivenKeyword_WhenSearchNotesByTitleQueryHandled_ThenOnlyCallerOwnedNotesReturned()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var otherUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(otherUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        await sender.SendAsync(new CreateNoteCommand("Roadmap 2026", "Content"), CancellationToken.None);

        userContext.AppUserId = otherUser.Id;
        await sender.SendAsync(new CreateNoteCommand("Roadmap 2026", "Content"), CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var result = await sender.SendAsync(new SearchNotesByTitleQuery("roadmap"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var found = Assert.Single(result.Value.Notes);
        Assert.Equal("Roadmap 2026", found.Title);
    }

    [Fact]
    public async Task GivenUserNotes_WhenGetNoteGraphQueryHandled_ThenNodesAndEdgesMatchOwnership()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var otherUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(otherUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var ownerTargetResult = await sender.SendAsync(new CreateNoteCommand("Owner target", "Content"), CancellationToken.None);
        var ownerSourceResult = await sender.SendAsync(new CreateNoteCommand("Owner source", "Content"), CancellationToken.None);
        await sender.SendAsync(
            new UpdateNoteCommand(
                ownerSourceResult.Value.NoteId,
                "Owner source",
                $"""<p>See <span data-note-link="{ownerTargetResult.Value.NoteId}">Owner target</span></p>"""),
            CancellationToken.None);

        userContext.AppUserId = otherUser.Id;
        await sender.SendAsync(new CreateNoteCommand("Other user's note", "Content"), CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var result = await sender.SendAsync(new GetNoteGraphQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Nodes.Count);
        Assert.DoesNotContain(result.Value.Nodes, n => n.Title == "Other user's note");
        var edge = Assert.Single(result.Value.Edges);
        Assert.Equal(ownerSourceResult.Value.NoteId, edge.SourceNoteId);
        Assert.Equal(ownerTargetResult.Value.NoteId, edge.TargetNoteId);
    }
}
