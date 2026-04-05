using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using sttp_ui.Models;

namespace sttp_ui.Services;

public sealed class OllamaService(
    HttpClient http,
    IOptions<OllamaOptions> options,
    ILogger<OllamaService> logger
)
{
    public async Task<AiSummary?> SummarizeAsync(string rawNode, CancellationToken ct = default)
    {
        var model = options.Value.Model;
        logger.LogInformation(
            "AI summary requested · model={Model} nodeLength={NodeLength}",
            model,
            rawNode.Length
        );

        var payload = new OllamaChatRequest(
            model,
            [new OllamaMessage("user", rawNode)],
            Stream: false
        );

        var sw = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/api/chat", payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AI summary HTTP request failed · model={Model} elapsed={ElapsedMs}ms",
                model,
                sw.ElapsedMilliseconds
            );
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "AI summary non-success response · model={Model} status={Status} elapsed={ElapsedMs}ms body={Body}",
                model,
                (int)response.StatusCode,
                sw.ElapsedMilliseconds,
                body
            );
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            cancellationToken: ct
        );
        sw.Stop();

        if (result?.Message?.Content is not { } text)
        {
            logger.LogWarning(
                "AI summary returned empty message · model={Model} elapsed={ElapsedMs}ms",
                model,
                sw.ElapsedMilliseconds
            );
            return null;
        }

        logger.LogDebug(
            "AI summary raw response · model={Model} elapsed={ElapsedMs}ms responseLength={ResponseLength}",
            model,
            sw.ElapsedMilliseconds,
            text.Length
        );

        var summary = ParseResponse(text);

        if (summary is null)
        {
            logger.LogWarning(
                "AI summary parse failed — no recognizable sections · model={Model} elapsed={ElapsedMs}ms",
                model,
                sw.ElapsedMilliseconds
            );
        }
        else
        {
            var missingSections = new[]
            {
                string.IsNullOrWhiteSpace(summary.Topic) ? "Topic" : null,
                string.IsNullOrWhiteSpace(summary.WhatHappened) ? "What happened" : null,
                string.IsNullOrWhiteSpace(summary.WhereWeLeftOff) ? "Where we left off" : null,
                string.IsNullOrWhiteSpace(summary.Vibe) ? "Vibe" : null,
                string.IsNullOrWhiteSpace(summary.PickBackUpWith) ? "Pick back up with" : null,
            }
                .Where(s => s is not null)
                .ToArray();

            if (missingSections.Length > 0)
                logger.LogWarning(
                    "AI summary parsed with missing sections · model={Model} elapsed={ElapsedMs}ms missing=[{Missing}]",
                    model,
                    sw.ElapsedMilliseconds,
                    string.Join(", ", missingSections)
                );
            else
                logger.LogInformation(
                    "AI summary complete · model={Model} elapsed={ElapsedMs}ms topic={Topic}",
                    model,
                    sw.ElapsedMilliseconds,
                    summary.Topic
                );
        }

        return summary;
    }

    private static AiSummary? ParseResponse(string text)
    {
        // Strip thinking blocks produced by reasoning models: "Thinking...\n...\n...done thinking."
        var cleaned = Regex
            .Replace(
                text,
                @"Thinking\.\.\..*?\.\.\.done thinking\.",
                "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            )
            .Trim();

        const string headings = @"Topic|What happened|Where we left off|Vibe|Pick back up with";

        string Extract(string label)
        {
            var m = Regex.Match(
                cleaned,
                $@"(?:^|\n){Regex.Escape(label)}:\s*(.+?)(?=\n(?:{headings}):|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        var topic = Extract("Topic");
        var what = Extract("What happened");
        var where = Extract("Where we left off");
        var vibe = Extract("Vibe");
        var pickup = Extract("Pick back up with");

        if (string.IsNullOrWhiteSpace(topic) && string.IsNullOrWhiteSpace(what))
            return null;

        return new AiSummary(topic, what, where, vibe, pickup);
    }
}

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream
);

internal sealed record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

internal sealed record OllamaChatResponse(
    [property: JsonPropertyName("message")] OllamaMessage? Message,
    [property: JsonPropertyName("done")] bool Done
);
