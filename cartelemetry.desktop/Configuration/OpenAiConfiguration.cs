namespace CarTelemetry.Desktop.Configuration;

public class OpenAiConfiguration
{
    public string Model { get; set; } = "gpt-5.2";
    public string VehicleModel { get; set; } = "Nissan 350Z Z33";
    public string? ApiKey { get; set; }
}
