// HTTP client imports for making REST API calls
using System.Net.Http;           // Core HTTP client functionality
using System.Net.Http.Json;      // JSON serialization extensions for HTTP

namespace CarTelemetry.Core;

/// <summary>
/// RelayPublisher is an HTTP API client that sends telemetry data to a remote web server.
/// This demonstrates the **Repository Pattern** and **HTTP API integration**.
/// 
/// KEY ARCHITECTURAL CONCEPTS:
/// 
/// 1. **Interface Implementation**: Implements ITelemetryPublisher interface
///    - This is **Dependency Inversion Principle** - high-level modules (AgentService) 
///      depend on abstractions (ITelemetryPublisher) not concrete classes
///    - Allows swapping different publishers (HTTP, file, database, etc.) without changing consumers
/// 
/// 2. **HTTP API Client Pattern**: 
///    - Encapsulates all HTTP communication logic in one place
///    - Handles authentication, serialization, retry logic, error handling
///    - Provides clean async API for sending data over the network
/// 
/// 3. **Dependency Injection Ready**:
///    - Constructor takes all dependencies as parameters
///    - No static dependencies or hard-coded URLs
///    - Configuration injected from appsettings.json via RelayConfiguration
/// 
/// 4. **Resource Management**: Implements IDisposable for HttpClient cleanup
///    - Proper resource lifecycle management
///    - Prevents memory leaks and connection pool exhaustion
/// 
/// 5. **Retry Logic**: Built-in resilience for network failures
///    - Exponential backoff strategy
///    - Graceful degradation under poor network conditions
/// </summary>
public sealed class RelayPublisher : ITelemetryPublisher, IDisposable
{
    // ===== DEPENDENCY INJECTION - CONSTRUCTOR PARAMETERS =====
    // These are the dependencies this service needs to function
    // Notice: ALL dependencies come through constructor - no static dependencies or globals
    private readonly HttpClient _http;      // HTTP client for making REST API calls
    private readonly string _vehicleId;     // Unique identifier for this vehicle (from config)
    private readonly string _sessionId;     // Unique identifier for this telemetry session (from config)
    private readonly string _ingestKey;     // API authentication key (from config)

    /// <summary>
    /// Constructor demonstrates **Dependency Injection** pattern.
    /// All external dependencies are provided as parameters rather than created internally.
    /// 
    /// DEPENDENCY INJECTION BENEFITS:
    /// 1. **Testability**: Can inject mock HttpClient for unit testing
    /// 2. **Flexibility**: Can configure different base URLs, authentication, etc.
    /// 3. **Separation of Concerns**: This class focuses on HTTP logic, not configuration
    /// 4. **Lifecycle Management**: Caller controls when HttpClient is created/disposed
    /// 
    /// CONFIGURATION FLOW:
    /// appsettings.json → RelayConfiguration → Program.cs DI setup → Constructor parameters
    /// </summary>
    /// <param name="baseAddress">Base URL of the relay server (e.g., https://api.example.com)</param>
    /// <param name="vehicleId">Vehicle identifier from configuration</param>
    /// <param name="sessionId">Session identifier from configuration</param>
    /// <param name="ingestKey">API authentication key from configuration</param>
    /// <param name="handler">Optional HTTP message handler (for testing/customization)</param>
    public RelayPublisher(Uri baseAddress, string vehicleId, string sessionId, string ingestKey, HttpMessageHandler? handler = null)
    {
        // Create HttpClient with optional custom handler (used for testing with mock responses)
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = baseAddress;  // Set base URL for all requests

        // Store configuration for use in API calls
        _vehicleId = vehicleId;
        _sessionId = sessionId;
        _ingestKey = ingestKey;
    }

    /// <summary>
    /// Publishes telemetry data to the relay server via HTTP POST.
    /// This demonstrates **REST API client implementation** with resilience patterns.
    /// 
    /// HTTP API CONCEPTS:
    /// 1. **REST Endpoint**: POST /ingest/{vehicleId}/{sessionId}
    /// 2. **JSON Serialization**: Converts .NET objects to JSON automatically
    /// 3. **Authentication**: Uses API key in custom header (X-Ingest-Key)
    /// 4. **HTTP Status Codes**: Checks for success (200-299 range)
    /// 5. **Async/Await**: Non-blocking network I/O operations
    /// 
    /// RESILIENCE PATTERNS:
    /// 1. **Retry Logic**: 3 attempts with exponential backoff
    /// 2. **Timeout Support**: CancellationToken for request timeouts
    /// 3. **Error Handling**: Graceful handling of network failures
    /// </summary>
    /// <param name="t">Telemetry data to send</param>
    /// <param name="ct">Cancellation token for timeout/cancellation</param>
    public async Task PublishAsync(Telemetry t, CancellationToken ct = default)
    {
        // ===== CREATE REQUEST PAYLOAD =====
        // Transform telemetry data into API-friendly format
        // This "envelope" pattern wraps the data with metadata
        var env = new
        {
            type = "telemetry",          // Message type identifier
            vehicleId = _vehicleId,      // Which vehicle sent this data
            sessionId = _sessionId,      // Which session this belongs to
            v = 1,                       // API version for future compatibility
            ts = t.TsUtcMs,             // Timestamp in UTC milliseconds
            payload = new { rpm = t.Rpm, speedKmh = t.SpeedKmh, coolantC = t.CoolantC } // Actual telemetry data
        };

        // ===== RETRY LOOP WITH EXPONENTIAL BACKOFF =====
        // Network calls can fail - implement resilience to handle temporary failures
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // ===== BUILD HTTP REQUEST =====
                var req = new HttpRequestMessage(HttpMethod.Post, $"/ingest/{_vehicleId}/{_sessionId}")
                {
                    Content = JsonContent.Create(env)  // Automatically serializes object to JSON
                };
                req.Headers.Add("X-Ingest-Key", _ingestKey);  // API authentication

                // ===== SEND REQUEST AND VALIDATE RESPONSE =====
                var res = await _http.SendAsync(req, ct);
                res.EnsureSuccessStatusCode();  // Throws exception if HTTP status indicates failure
                break; // Success - exit retry loop
            }
            catch when (attempt < 3)  // Only catch and retry if we have attempts left
            {
                // Exponential backoff: wait longer after each failure
                // Attempt 1: 250ms, Attempt 2: 500ms delay before final attempt
                await Task.Delay(250 * attempt, ct);
            }
            // Final attempt (attempt 3) - let exception bubble up to caller
        }
    }

    /// <summary>
    /// IDisposable implementation for proper resource cleanup.
    /// This is critical for HttpClient - prevents connection pool exhaustion and memory leaks.
    /// 
    /// RESOURCE MANAGEMENT:
    /// - HttpClient holds onto network connections and internal resources
    /// - Must be disposed when no longer needed
    /// - Called automatically by DI container when application shuts down
    /// </summary>
    public void Dispose() => _http.Dispose();
}
