using System;
using System.Collections.Generic;

namespace CarTelemetry.WebApp.Configuration;

/// <summary>
/// API keys accepted by the web telemetry ingest endpoint.
/// </summary>
public class IngestConfiguration
{
    public Dictionary<string, string> IngestKeys { get; set; } = new();
}

public class CorsConfiguration  
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

