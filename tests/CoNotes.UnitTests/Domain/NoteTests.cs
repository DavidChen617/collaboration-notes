using CoNotes.Domain.Notes.Events;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Domain;

public class NoteTests
{
    [Fact]
    public void GivenValidInput_WhenCreatingANote_ThenRaisesNoteCreatedEvent()
    {
        var ownerAppUserId = Guid.NewGuid();

        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        Assert.Equal(ownerAppUserId, note.OwnerAppUserId);
        Assert.Equal("Title", note.Title);
        Assert.Equal("Content", note.Content);

        var domainEvent = Assert.Single(note.DomainEvents);
        var createdEvent = Assert.IsType<NoteCreatedDomainEvent>(domainEvent);
        Assert.Equal(note.Id, createdEvent.NoteId);
        Assert.Equal(ownerAppUserId, createdEvent.OwnerAppUserId);
    }

    [Fact]
    public void GivenOwner_WhenUpdating_ThenSucceedsAndAppliesChanges()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.Update(ownerAppUserId, "New title", "New content", DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", note.Title);
        Assert.Equal("New content", note.Content);
    }

    [Fact]
    public void GivenNonOwner_WhenUpdating_ThenFailsAndLeavesNoteUnchanged()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.Update(otherAppUserId, "New title", "New content", DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Update", result.Error.Code);
        Assert.Equal("Title", note.Title);
        Assert.Equal("Content", note.Content);
    }

    [Fact]
    public void GivenLinkTargetOwnedBySameUser_WhenResolveLinks_ThenLinkAccepted()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var targetNoteId = Guid.NewGuid();

        var result = note.ResolveLinks([targetNoteId], new HashSet<Guid> { targetNoteId });

        Assert.True(result.IsSuccess);
        Assert.Equal([targetNoteId], note.LinkedNoteIds);
    }

    [Fact]
    public void GivenLinkTargetOwnedByAnotherUser_WhenResolveLinks_ThenLinkRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var targetNoteId = Guid.NewGuid();

        var result = note.ResolveLinks([targetNoteId], new HashSet<Guid>());

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.ResolveLinks", result.Error.Code);
        Assert.Empty(note.LinkedNoteIds);
    }

    [Fact]
    public void GivenNoteContentChanged_WhenLinksResolved_ThenNoteLinkedToEventsRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var targetNoteId = Guid.NewGuid();

        var result = note.ResolveLinks([targetNoteId], new HashSet<Guid> { targetNoteId });

        Assert.True(result.IsSuccess);
        var linkedEvent = Assert.IsType<NoteLinkedToDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteLinkedToDomainEvent));
        Assert.Equal(note.Id, linkedEvent.SourceNoteId);
        Assert.Equal(targetNoteId, linkedEvent.TargetNoteId);
    }

    [Fact]
    public void GivenLinkRemovedFromContent_WhenLinksResolved_ThenNoteLinkRemovedEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var targetNoteId = Guid.NewGuid();
        note.ResolveLinks([targetNoteId], new HashSet<Guid> { targetNoteId });

        var result = note.ResolveLinks([], new HashSet<Guid>());

        Assert.True(result.IsSuccess);
        var removedEvent = Assert.IsType<NoteLinkRemovedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteLinkRemovedDomainEvent));
        Assert.Equal(note.Id, removedEvent.SourceNoteId);
        Assert.Equal(targetNoteId, removedEvent.TargetNoteId);
        Assert.Empty(note.LinkedNoteIds);
    }

    [Fact]
    public void GivenOwner_WhenDeleting_ThenSucceedsAndRaisesNoteDeletedEvent()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.Delete(ownerAppUserId);

        Assert.True(result.IsSuccess);
        var deletedEvent = Assert.IsType<NoteDeletedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteDeletedDomainEvent));
        Assert.Equal(note.Id, deletedEvent.NoteId);
    }

    [Fact]
    public void GivenNonOwner_WhenDeleting_ThenFails()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.Delete(otherAppUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Delete", result.Error.Code);
    }

    [Fact]
    public void GivenOwner_WhenGeneratingShareLink_ThenShareTokenCreatedAndEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.GenerateShareLink(ownerAppUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, note.ShareToken);
        Assert.NotEqual(Guid.Empty, result.Value);
        var generatedEvent = Assert.IsType<NoteShareLinkGeneratedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteShareLinkGeneratedDomainEvent));
        Assert.Equal(note.Id, generatedEvent.NoteId);
        Assert.Equal(result.Value, generatedEvent.ShareToken);
    }

    [Fact]
    public void GivenNonOwner_WhenGeneratingShareLink_ThenRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.GenerateShareLink(otherAppUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.GenerateShareLink", result.Error.Code);
        Assert.Null(note.ShareToken);
    }

    [Fact]
    public void GivenActiveShareLink_WhenRevoked_ThenTokenInvalidatedButExistingCollaboratorsUnaffected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var oldToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(oldToken, collaboratorAppUserId);

        var result = note.RevokeShareLink(ownerAppUserId);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldToken, result.Value);
        Assert.Equal(result.Value, note.ShareToken);
        Assert.Contains(collaboratorAppUserId, note.CollaboratorAppUserIds);

        var joinWithOldToken = note.JoinViaShareLink(oldToken, Guid.NewGuid());
        Assert.False(joinWithOldToken.IsSuccess);
    }

    [Fact]
    public void GivenValidShareToken_WhenUserJoins_ThenAddedToCollaboratorListAndEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var joiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = note.GenerateShareLink(ownerAppUserId).Value;

        var result = note.JoinViaShareLink(shareToken, joiningAppUserId);

        Assert.True(result.IsSuccess);
        Assert.Contains(joiningAppUserId, note.CollaboratorAppUserIds);
        var joinedEvent = Assert.IsType<NoteCollaboratorJoinedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteCollaboratorJoinedDomainEvent));
        Assert.Equal(note.Id, joinedEvent.NoteId);
        Assert.Equal(joiningAppUserId, joinedEvent.AppUserId);
    }

    [Fact]
    public void GivenSameUserJoinsTwice_WhenUsingSameShareToken_ThenNoDuplicateOrExtraEvent()
    {
        var ownerAppUserId = Guid.NewGuid();
        var joiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(shareToken, joiningAppUserId);

        var result = note.JoinViaShareLink(shareToken, joiningAppUserId);

        Assert.True(result.IsSuccess);
        Assert.Single(note.CollaboratorAppUserIds);
        Assert.Single(note.DomainEvents, e => e is NoteCollaboratorJoinedDomainEvent);
    }

    [Fact]
    public void GivenOwner_WhenRemovingCollaborator_ThenRemovedAndEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var result = note.RemoveCollaborator(ownerAppUserId, collaboratorAppUserId);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(collaboratorAppUserId, note.CollaboratorAppUserIds);
        var removedEvent = Assert.IsType<NoteCollaboratorRemovedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteCollaboratorRemovedDomainEvent));
        Assert.Equal(note.Id, removedEvent.NoteId);
        Assert.Equal(collaboratorAppUserId, removedEvent.AppUserId);
    }

    [Fact]
    public void GivenNonOwner_WhenRemovingCollaborator_ThenRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var result = note.RemoveCollaborator(collaboratorAppUserId, collaboratorAppUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.RemoveCollaborator", result.Error.Code);
        Assert.Contains(collaboratorAppUserId, note.CollaboratorAppUserIds);
    }

    [Fact]
    public void GivenCollaborator_WhenUpdating_ThenSucceeds()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = note.GenerateShareLink(ownerAppUserId).Value;
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var result = note.Update(collaboratorAppUserId, "New title", "New content", DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", note.Title);
    }
}
