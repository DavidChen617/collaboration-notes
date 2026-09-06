using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoNotes.Infrastructure.ChatMessages.Providers;

internal sealed class GeminiChatProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GeminiChatProvider> logger
) : IAiChatProvider
{
    public string Name => "Gemini";

    public async Task<AiChatReply?> TryGetReplyAsync(AiChatContext context, CancellationToken ct)
    {
        var apiKey = configuration["Ai:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var model = configuration["Ai:Gemini:Model"] ?? "gemini-2.0-flash";
        var endpoint = configuration["Ai:Gemini:Endpoint"]
            ?? $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var separator = endpoint.Contains('?') ? "&" : "?";
        var requestUri = $"{endpoint}{separator}key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                requestUri,
                new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = AiPromptBuilder.Build(context) } },
                        },
                    },
                },
                ct
            );
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
            var content = body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return string.IsNullOrWhiteSpace(content) ? null : new AiChatReply(content);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Gemini provider request failed");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini provider request timed out");
            return null;
        }
    }

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates
    );

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content
    );

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart>? Parts
    );

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string? Text
    );
}
