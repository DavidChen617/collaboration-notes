using System.Text.RegularExpressions;

namespace CoNotes.Application.Notes;

/// <summary>
/// 解析筆記 HTML content 內嵌的 wikilink, 取出所有被連結的目標筆記 ID。
/// 前端的 link node 會 render 成 &lt;span data-note-link="{noteId}"&gt;{title}&lt;/span&gt;
/// (見 note-linking design.md decision 2), 這裡是唯一解析回這個格式的地方。
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
