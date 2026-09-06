using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoNotes.Infrastructure.ChatMessages.Providers;

internal sealed class GroqChatProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GroqChatProvider> logger
) : IAiChatProvider
{
    public string Name => "Groq";

    public async Task<AiChatReply?> TryGetReplyAsync(AiChatContext context, CancellationToken ct)
    {
        var apiKey = configuration["Ai:Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var endpoint = configuration["Ai:Groq:Endpoint"]
            ?? "https://api.groq.com/openai/v1/chat/completions";
        var model = configuration["Ai:Groq:Model"] ?? "llama-3.3-70b-versatile";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            messages = new[] { new { role = "user", content = AiPromptBuilder.Build(context) } },
        });

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<GroqResponse>(ct);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
            return string.IsNullOrWhiteSpace(content) ? null : new AiChatReply(content);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Groq provider request failed");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Groq provider request timed out");
            return null;
        }
    }

    private sealed record GroqResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<GroqChoice>? Choices
    );

    private sealed record GroqChoice(
        [property: JsonPropertyName("message")] GroqMessage? Message
    );

    private sealed record GroqMessage(
        [property: JsonPropertyName("content")] string? Content
    );
}
