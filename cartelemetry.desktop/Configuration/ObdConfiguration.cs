namespace CarTelemetry.Desktop.Configuration;

public class ObdConfiguration
{
    public string? PortName { get; set; }
    public int Baud { get; set; } = 38400;
    public bool UseMock { get; set; } = true;
}
