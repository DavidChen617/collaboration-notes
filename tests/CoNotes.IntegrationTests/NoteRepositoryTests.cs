using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Notes;
using Microsoft.Extensions.DependencyInjection;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class NoteRepositoryTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_the_persisted_note()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var noteRepository = scope.ServiceProvider.GetRequiredService<INoteRepository>();

        var appUser = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        await appUserRepository.AddAsync(appUser, CancellationToken.None);

        var note = NoteAggregate.Create(appUser.Id, "Title", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var loaded = await noteRepository.GetByIdAsync(note.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(appUser.Id, loaded.OwnerAppUserId);
        Assert.Equal("Title", loaded.Title);
        Assert.Equal("Content", loaded.Content);
    }

    [Fact]
    public async Task UpdateAsync_persists_the_new_title_and_content()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var noteRepository = scope.ServiceProvider.GetRequiredService<INoteRepository>();

        var appUser = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        await appUserRepository.AddAsync(appUser, CancellationToken.None);

        var note = NoteAggregate.Create(appUser.Id, "Title", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(note, CancellationToken.None);

        note.Update(appUser.Id, "New title", "New content", DateTime.UtcNow);
        await noteRepository.UpdateAsync(note, CancellationToken.None);

        var reloaded = await noteRepository.GetByIdAsync(note.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("New title", reloaded.Title);
        Assert.Equal("New content", reloaded.Content);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_note()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var noteRepository = scope.ServiceProvider.GetRequiredService<INoteRepository>();

        var appUser = AppUser.Create(Guid.NewGuid(, DateTime.UtcNow).ToString());
        await appUserRepository.AddAsync(appUser, CancellationToken.None);

        var note = NoteAggregate.Create(appUser.Id, "Title", "Content", DateTime.UtcNow);
        await noteRepository.AddAsync(note, CancellationToken.None);

        note.Delete(appUser.Id);
        await noteRepository.DeleteAsync(note, CancellationToken.None);

        var afterDelete = await noteRepository.GetByIdAsync(note.Id, CancellationToken.None);

        Assert.Null(afterDelete);
    }
}
