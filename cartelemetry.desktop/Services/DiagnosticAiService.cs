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

/// <summary>
/// Produces a concise diagnostic analysis for the current vehicle and DTC set.
/// </summary>
public interface IDiagnosticAiService
{
    Task<string> AnalyzeAsync(string vehicleModel, IReadOnlyCollection<DtcCode> codes, CancellationToken ct);
}

public sealed class OpenAiDiagnosticService : IDiagnosticAiService, IDisposable
{
    private readonly OpenAiConfiguration _config;
    private readonly IDiagnosticAnalysisCacheService _cache;
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://api.openai.com")
    };

    public OpenAiDiagnosticService(OpenAiConfiguration config, IDiagnosticAnalysisCacheService cache)
    {
        _config = config;
        _cache = cache;
    }

    public async Task<string> AnalyzeAsync(string vehicleModel, IReadOnlyCollection<DtcCode> codes, CancellationToken ct)
    {
        if (codes.Count == 0)
        {
            return "No diagnostic trouble codes were found to analyze.";
        }

        var cacheKey = _cache.CreateKey(_config.Model, vehicleModel, codes);
        var cachedAnalysis = await _cache.GetAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cachedAnalysis))
        {
            return cachedAnalysis;
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "OpenAI API key is not configured. Set the OPENAI_API_KEY environment variable, then restart the app.";
        }

        // Responses API output is constrained to a stable format so the UI can display it directly.
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

        var analysis = ExtractOutputText(body);
        await _cache.SetAsync(cacheKey, analysis, ct);
        return analysis;
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
        sb.AppendLine(@"You are an automotive diagnostic assistant inside a vehicle telemetry system.

Always respond using this exact format:

Issue:
[Plain-English summary of the detected issue or code]

Severity:
[Low / Medium / High / Critical]

What it means:
[Explain what the code or symptom usually means in simple terms]

Likely causes:
1. [Most likely cause]
2. [Second likely cause]
3. [Third likely cause]

What to check first:
1. [First practical inspection/test]
2. [Second practical inspection/test]
3. [Third practical inspection/test]

Can I keep driving?
[Yes / Short distance only / No — explain briefly]

Estimated urgency:
[Explain how soon this should be inspected]

Notes:
[Important warning, uncertainty, or car-specific context]

Disclaimer:
This information is automatically generated based on available vehicle data and is for informational purposes only. It is not a substitute for a professional mechanic inspection. The system may be incomplete or inaccurate. Always verify critical issues with a qualified technician before making repair or driving decisions.

Rules:
- Respond ONLY in this format
- Do NOT add extra sections
- Do NOT include markdown formatting
- If information is missing, say ""Not enough data provided.""");
        return sb.ToString();
    }

    private static string ExtractOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Newer Responses API responses may include a flattened output_text convenience field.
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


