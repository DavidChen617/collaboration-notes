using CoNotes.Domain.Notes.Events;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace UnitTests.Domain;

public class NoteTests
{
    [Fact]
    public void GivenValidInput_WhenCreatingANote_ThenRaisesNoteCreatedEvent()
    {
        var ownerAppUserId = Guid.NewGuid();

        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

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
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

        var result = note.Update(ownerAppUserId, "New title", "New content");

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", note.Title);
        Assert.Equal("New content", note.Content);
    }

    [Fact]
    public void GivenNonOwner_WhenUpdating_ThenFailsAndLeavesNoteUnchanged()
    {
        var ownerAppUserId = Guid.NewGuid();
        var otherAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

        var result = note.Update(otherAppUserId, "New title", "New content");

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Update", result.Error.Code);
        Assert.Equal("Title", note.Title);
        Assert.Equal("Content", note.Content);
    }

    [Fact]
    public void GivenLinkTargetOwnedBySameUser_WhenResolveLinks_ThenLinkAccepted()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");
        var targetNoteId = Guid.NewGuid();

        var result = note.ResolveLinks([targetNoteId], new HashSet<Guid> { targetNoteId });

        Assert.True(result.IsSuccess);
        Assert.Equal([targetNoteId], note.LinkedNoteIds);
    }

    [Fact]
    public void GivenLinkTargetOwnedByAnotherUser_WhenResolveLinks_ThenLinkRejected()
    {
        var ownerAppUserId = Guid.NewGuid();
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");
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
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");
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
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");
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
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

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
        var note = NoteAggregate.Create(ownerAppUserId, "Title", "Content");

        var result = note.Delete(otherAppUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Note.Delete", result.Error.Code);
    }
}
