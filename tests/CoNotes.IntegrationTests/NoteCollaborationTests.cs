using System.Text;
using CoNotes.Application.Abstractions;
using CoNotes.Application.Notes.Commands.Create;
using CoNotes.Application.Notes.Commands.Join;
using CoNotes.Application.Notes.Commands.Share;
using CoNotes.Application.Notes.Commands.Update;
using CoNotes.Application.Notes.Queries.Get;
using CoNotes.Application.Notes.Queries.List;
using CoNotes.Domain.AppUsers;
using Davish.Sendr;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class NoteCollaborationTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenShareTokenAndCollaboratorRows_WhenQueried_ThenAccessCheckMatchesOwnerOrCollaborator()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        owner.ApplyRedeemedPlanTier(PlanTier.Pro);
        var collaborator = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        var unrelatedUser = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);
        await appUserRepository.AddAsync(collaborator, CancellationToken.None);
        await appUserRepository.AddAsync(unrelatedUser, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var createResult = await sender.SendAsync(new CreateNoteCommand("Shared note", "Content"), CancellationToken.None);
        var noteId = createResult.Value.NoteId;

        var shareLinkResult = await sender.SendAsync(new GenerateShareLinkCommand(noteId), CancellationToken.None);
        var shareToken = shareLinkResult.Value.ShareToken;

        userContext.AppUserId = collaborator.Id;
        await sender.SendAsync(new JoinNoteViaShareLinkCommand(shareToken), CancellationToken.None);

        var collaboratorGetResult = await sender.SendAsync(new GetNoteQuery(noteId), CancellationToken.None);
        Assert.True(collaboratorGetResult.IsSuccess);

        var collaboratorListResult = await sender.SendAsync(new ListNotesQuery(), CancellationToken.None);
        Assert.True(collaboratorListResult.IsSuccess);
        Assert.Contains(collaboratorListResult.Value.Notes, n => n.NoteId == noteId);

        var collaboratorUpdateResult = await sender.SendAsync(
            new UpdateNoteCommand(noteId, "Updated by collaborator", "New content"), CancellationToken.None);
        Assert.True(collaboratorUpdateResult.IsSuccess);

        userContext.AppUserId = unrelatedUser.Id;

        var unrelatedGetResult = await sender.SendAsync(new GetNoteQuery(noteId), CancellationToken.None);
        Assert.False(unrelatedGetResult.IsSuccess);

        var unrelatedListResult = await sender.SendAsync(new ListNotesQuery(), CancellationToken.None);
        Assert.True(unrelatedListResult.IsSuccess);
        Assert.DoesNotContain(unrelatedListResult.Value.Notes, n => n.NoteId == noteId);

        var unrelatedUpdateResult = await sender.SendAsync(
            new UpdateNoteCommand(noteId, "Should not apply", "Should not apply"), CancellationToken.None);
        Assert.False(unrelatedUpdateResult.IsSuccess);
    }

    [Fact]
    public async Task GivenSnapshotAndSubsequentUpdates_WhenReplayed_ThenReconstructsContentAtGivenPoint()
    {
        using var scope = factory.Services.CreateScope();
        var appUserRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userContext = scope.ServiceProvider.GetRequiredService<TestUserContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var editHistoryStore = scope.ServiceProvider.GetRequiredService<INoteEditHistoryStore>();

        var owner = AppUser.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);
        await appUserRepository.AddAsync(owner, CancellationToken.None);

        userContext.AppUserId = owner.Id;
        var createResult = await sender.SendAsync(new CreateNoteCommand("History note", "Content"), CancellationToken.None);
        var noteId = createResult.Value.NoteId;

        var beforeAnything = DateTime.UtcNow;
        await Task.Delay(10);

        await editHistoryStore.AppendUpdateAsync(noteId, Encoding.UTF8.GetBytes("update-1"), CancellationToken.None);
        await Task.Delay(10);

        var snapshotPayload = Encoding.UTF8.GetBytes("snapshot-after-update-1");
        await editHistoryStore.SaveSnapshotAsync(noteId, snapshotPayload, CancellationToken.None);
        var afterSnapshot = DateTime.UtcNow;
        await Task.Delay(10);

        await editHistoryStore.AppendUpdateAsync(noteId, Encoding.UTF8.GetBytes("update-2"), CancellationToken.None);
        var afterUpdate2 = DateTime.UtcNow;

        var historyBeforeAnything = await editHistoryStore.GetHistoryUpToAsync(noteId, beforeAnything, CancellationToken.None);
        Assert.Null(historyBeforeAnything.BaseSnapshotPayload);
        Assert.Empty(historyBeforeAnything.SubsequentUpdatePayloads);

        var historyAtSnapshot = await editHistoryStore.GetHistoryUpToAsync(noteId, afterSnapshot, CancellationToken.None);
        Assert.Equal(snapshotPayload, historyAtSnapshot.BaseSnapshotPayload);
        Assert.Empty(historyAtSnapshot.SubsequentUpdatePayloads);

        var historyAtLatest = await editHistoryStore.GetHistoryUpToAsync(noteId, afterUpdate2, CancellationToken.None);
        Assert.Equal(snapshotPayload, historyAtLatest.BaseSnapshotPayload);
        var subsequentUpdate = Assert.Single(historyAtLatest.SubsequentUpdatePayloads);
        Assert.Equal("update-2", Encoding.UTF8.GetString(subsequentUpdate));
    }
}
