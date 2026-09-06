namespace CoNotes.Infrastructure.ChatMessages.Providers;

internal static class AiPromptBuilder
{
    public static string Build(AiChatContext context)
    {
        var history = string.Join(
            Environment.NewLine,
            context.RecentMessages.Select(message =>
                $"{(message.IsAiReply ? "AI" : "User")}: {message.Content}"
            )
        );

        return $"""
            You are an assistant helping users collaborate on a note.
            Note content:
            {context.NoteContent}

            Recent chat history:
            {history}

            Answer the user's latest request concisely and use the note content when relevant.
            """;
    }
}
