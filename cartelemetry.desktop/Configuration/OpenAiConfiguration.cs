namespace CarTelemetry.Desktop.Configuration;

/// <summary>
/// OpenAI diagnostic assistant settings for model selection, vehicle context, and credentials.
/// </summary>
public class OpenAiConfiguration
{
    public string Model { get; set; } = "gpt-5.2";
    public string VehicleModel { get; set; } = "Nissan 350Z Z33";
    public string? ApiKey { get; set; }
}

