using System.Text.RegularExpressions;

namespace CoNotes.Application.Notes;

/// <summary>
/// Extracts the target note IDs referenced by wikilinks embedded in a note's HTML content.
/// The frontend's link node renders as &lt;span data-note-link="{noteId}"&gt;{title}&lt;/span&gt;
/// (see note-linking design.md decision 2); this is the one place that format is parsed back out.
/// </summary>
internal static partial class NoteLinkContentParser
{
    public static IReadOnlyCollection<Guid> ExtractLinkedNoteIds(string content)
    {
        var targetNoteIds = new HashSet<Guid>();

        foreach (Match match in DataNoteLinkPattern().Matches(content))
        {
            if (Guid.TryParse(match.Groups[1].Value, out var targetNoteId))
                targetNoteIds.Add(targetNoteId);
        }

        return targetNoteIds;
    }

    [GeneratedRegex("data-note-link=\"([0-9a-fA-F-]{36})\"")]
    private static partial Regex DataNoteLinkPattern();
}
