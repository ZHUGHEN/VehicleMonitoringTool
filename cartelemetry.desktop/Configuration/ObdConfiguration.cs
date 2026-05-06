namespace CarTelemetry.Desktop.Configuration;

/// <summary>
/// Serial adapter settings for the active OBD-II data source.
/// </summary>
public class ObdConfiguration
{
    public string? PortName { get; set; }
    public int Baud { get; set; } = 38400;
    public bool UseMock { get; set; } = true;
}

