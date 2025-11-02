namespace CarTelemetry.Core;

/// <summary>
/// ITelemetryPublisher defines the contract for sending telemetry data to external systems.
/// This interface demonstrates **Strategy Pattern** and **Dependency Injection** principles.
/// 
/// KEY ARCHITECTURAL CONCEPTS:
/// 
/// 1. **Single Responsibility Principle**: 
///    - Interface has one job: publish telemetry data
///    - Doesn't care about HOW or WHERE data is published
///    - Clean separation between data generation and data transmission
/// 
/// 2. **Strategy Pattern**: 
///    - Different implementations handle different destinations
///    - RelayPublisher: HTTP API to remote server
///    - FilePublisher: Local file storage  
///    - DatabasePublisher: Direct database writes
///    - NullPublisher: Discard data (testing/debugging)
/// 
/// 3. **Dependency Inversion**: 
///    - AgentService depends on this abstraction, not concrete publishers
///    - Allows swapping publishers without changing AgentService code
///    - Configuration determines which publisher is used at runtime
/// 
/// 4. **Testability**: 
///    - Easy to create mock publishers for unit testing
///    - Can verify publish calls without actual network/file I/O
///    - Enables isolated testing of AgentService logic
/// 
/// IMPLEMENTATION EXAMPLES:
/// 
/// ```csharp
/// // HTTP API Publisher (production)
/// services.AddSingleton<ITelemetryPublisher>(_ => 
///     new RelayPublisher(serverUrl, vehicleId, sessionId, apiKey));
/// 
/// // File Publisher (offline mode)
/// services.AddSingleton<ITelemetryPublisher>(_ => 
///     new FilePublisher(@"C:\telemetry\data.json"));
/// 
/// // Null Publisher (testing)
/// services.AddSingleton<ITelemetryPublisher>(_ => 
///     new NullPublisher());
/// ```
/// 
/// DEPENDENCY INJECTION FLOW:
/// Program.cs → DI Container → AgentService constructor → ITelemetryPublisher
/// </summary>
public interface ITelemetryPublisher
{
    /// <summary>
    /// Publishes a single telemetry reading to the configured destination.
    /// 
    /// ASYNC DESIGN:
    /// - Publishing often involves I/O (network, file, database)
    /// - Async prevents blocking the calling thread
    /// - Allows multiple publishes to happen concurrently
    /// 
    /// CANCELLATION SUPPORT:
    /// - CancellationToken allows graceful shutdown
    /// - Can timeout long-running publish operations
    /// - Enables responsive application shutdown
    /// 
    /// ERROR HANDLING:
    /// - Implementations should handle their own retries
    /// - Throw exceptions for unrecoverable errors
    /// - Log failures for debugging and monitoring
    /// </summary>
    /// <param name="t">Telemetry data to publish</param>
    /// <param name="ct">Cancellation token for timeout/shutdown</param>
    /// <returns>Task that completes when publish is finished</returns>
    Task PublishAsync(Telemetry t, CancellationToken ct = default);
}
