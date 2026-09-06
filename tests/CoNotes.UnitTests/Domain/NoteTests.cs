using CoNotes.Domain.Notes;
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
    public void GivenOwner_WhenCheckingOwnership_ThenTrue()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        Assert.True(note.IsOwnedBy(ownerAppUserId));
    }

    [Fact]
    public void GivenNonOwner_WhenCheckingOwnership_ThenFalse()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        Assert.False(note.IsOwnedBy(Guid.NewGuid()));
    }

    [Fact]
    public void GivenOwnerOrCollaborator_WhenCheckingAccessibility_ThenTrue()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = CreateShareToken();
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        Assert.True(note.IsAccessibleBy(ownerAppUserId));
        Assert.True(note.IsAccessibleBy(collaboratorAppUserId));
    }

    [Fact]
    public void GivenUnrelatedUser_WhenCheckingAccessibility_ThenFalse()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        Assert.False(note.IsAccessibleBy(Guid.NewGuid()));
    }

    [Fact]
    public void GivenValidInput_WhenUpdating_ThenAppliesChanges()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        note.Update("New title", "New content", DateTime.UtcNow);

        Assert.Equal("New title", note.Title);
        Assert.Equal("New content", note.Content);
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
    public void GivenValidInput_WhenDeleting_ThenRaisesNoteDeletedEvent()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        note.Delete();

        var deletedEvent = Assert.IsType<NoteDeletedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteDeletedDomainEvent));
        Assert.Equal(note.Id, deletedEvent.NoteId);
    }

    [Fact]
    public void GivenValidInput_WhenSettingShareLink_ThenShareTokenStoredAndEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = CreateShareToken();

        note.SetShareLink(shareToken);

        Assert.Equal(shareToken, note.ShareToken);
        var generatedEvent = Assert.IsType<NoteShareLinkGeneratedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteShareLinkGeneratedDomainEvent));
        Assert.Equal(note.Id, generatedEvent.NoteId);
        Assert.Equal(shareToken, generatedEvent.ShareToken);
    }

    [Fact]
    public void GivenActiveShareLink_WhenRevoked_ThenTokenInvalidatedButExistingCollaboratorsUnaffected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var oldToken = CreateShareToken();
        note.SetShareLink(oldToken);
        note.JoinViaShareLink(oldToken, collaboratorAppUserId);

        note.RevokeShareLink();

        Assert.Null(note.ShareToken);
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
        var shareToken = CreateShareToken();
        note.SetShareLink(shareToken);

        var result = note.JoinViaShareLink(shareToken, joiningAppUserId);

        Assert.True(result.IsSuccess);
        Assert.Contains(joiningAppUserId, note.CollaboratorAppUserIds);
        var joinedEvent = Assert.IsType<NoteCollaboratorJoinedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteCollaboratorJoinedDomainEvent));
        Assert.Equal(note.Id, joinedEvent.NoteId);
        Assert.Equal(joiningAppUserId, joinedEvent.AppUserId);
    }

    [Fact]
    public void GivenInvalidShareToken_WhenUserJoins_ThenRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var joiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        note.SetShareLink(CreateShareToken());

        var result = note.JoinViaShareLink(CreateShareToken(), joiningAppUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.JoinViaShareLink", result.Error.Code);
        Assert.DoesNotContain(joiningAppUserId, note.CollaboratorAppUserIds);
    }

    [Fact]
    public void GivenSameUserJoinsTwice_WhenUsingSameShareToken_ThenNoDuplicateOrExtraEvent()
    {
        var ownerAppUserId = Guid.NewGuid();
        var joiningAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = CreateShareToken();
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, joiningAppUserId);

        var result = note.JoinViaShareLink(shareToken, joiningAppUserId);

        Assert.True(result.IsSuccess);
        Assert.Single(note.CollaboratorAppUserIds);
        Assert.Single(note.DomainEvents, e => e is NoteCollaboratorJoinedDomainEvent);
    }

    [Fact]
    public void GivenExistingCollaborator_WhenRemovingCollaborator_ThenRemovedAndEventRaised()
    {
        var ownerAppUserId = Guid.NewGuid();
        var collaboratorAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);
        var shareToken = CreateShareToken();
        note.SetShareLink(shareToken);
        note.JoinViaShareLink(shareToken, collaboratorAppUserId);

        var result = note.RemoveCollaborator(collaboratorAppUserId);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(collaboratorAppUserId, note.CollaboratorAppUserIds);
        var removedEvent = Assert.IsType<NoteCollaboratorRemovedDomainEvent>(Assert.Single(
            note.DomainEvents, e => e is NoteCollaboratorRemovedDomainEvent));
        Assert.Equal(note.Id, removedEvent.NoteId);
        Assert.Equal(collaboratorAppUserId, removedEvent.AppUserId);
    }

    [Fact]
    public void GivenNotACollaborator_WhenRemovingCollaborator_ThenRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content", DateTime.UtcNow);

        var result = note.RemoveCollaborator(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.RemoveCollaborator", result.Error.Code);
    }

    private static ShareLinkToken CreateShareToken() => new(Guid.NewGuid().ToString());
}
