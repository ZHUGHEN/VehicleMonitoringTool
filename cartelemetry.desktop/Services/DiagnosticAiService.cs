using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.Configuration;

namespace CarTelemetry.Desktop.Services;

public interface IDiagnosticAiService
{
    Task<string> AnalyzeAsync(string vehicleModel, IReadOnlyCollection<DtcCode> codes, CancellationToken ct);
}

public sealed class OpenAiDiagnosticService : IDiagnosticAiService, IDisposable
{
    private readonly OpenAiConfiguration _config;
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://api.openai.com")
    };

    public OpenAiDiagnosticService(OpenAiConfiguration config)
    {
        _config = config;
    }

    public async Task<string> AnalyzeAsync(string vehicleModel, IReadOnlyCollection<DtcCode> codes, CancellationToken ct)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "OpenAI API key is not configured. Set the OPENAI_API_KEY environment variable, then restart the app.";
        }

        if (codes.Count == 0)
        {
            return "No diagnostic trouble codes were found to analyze.";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
        {
            Content = JsonContent.Create(new
            {
                model = _config.Model,
                instructions = """
                    You are an experienced automotive diagnostic assistant. Help the user reason about possible causes from OBD-II diagnostic trouble codes.
                    Keep the answer practical and prioritized. Include likely causes, checks to perform first, and what not to replace blindly.
                    Do not claim certainty from DTCs alone. Remind the user to verify with live data, service manual procedures, and safe workshop practices.
                    """,
                input = BuildPrompt(vehicleModel, codes),
                max_output_tokens = 900
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return $"ChatGPT analysis failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}";
        }

        return ExtractOutputText(body);
    }

    public void Dispose() => _http.Dispose();

    private string? GetApiKey()
        => string.IsNullOrWhiteSpace(_config.ApiKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : _config.ApiKey;

    private static string BuildPrompt(string vehicleModel, IReadOnlyCollection<DtcCode> codes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Vehicle model: {vehicleModel}");
        sb.AppendLine("Diagnostic trouble codes:");

        foreach (var code in codes.OrderBy(c => c.Code))
        {
            sb.AppendLine($"- {code.Code} ({code.System}): {code.Description ?? "No local description available"}");
        }

        sb.AppendLine();
        sb.AppendLine("Please diagnose the most likely root causes and suggest a step-by-step inspection plan.");
        return sb.ToString();
    }

    private static string ExtractOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return "ChatGPT returned a response, but no text output was found.";
        }

        var sb = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    sb.AppendLine(text.GetString());
                }
            }
        }

        return sb.Length == 0
            ? "ChatGPT returned a response, but no text output was found."
            : sb.ToString().Trim();
    }
}
