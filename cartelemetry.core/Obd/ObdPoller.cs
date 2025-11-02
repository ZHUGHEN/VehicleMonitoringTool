// System imports for async streaming and collections
using System;
using System.Collections.Generic;  // For IAsyncEnumerable interface
using System.Threading;           // For CancellationToken support
using System.Threading.Tasks;     // For async/await operations

namespace CarTelemetry.Core.Obd;

/// <summary>
/// Interface defining the contract for OBD data polling.
/// This demonstrates **Interface Segregation Principle** - clients only depend on what they need.
/// 
/// ASYNC ENUMERABLE PATTERN:
/// IAsyncEnumerable&lt;T&gt; is a powerful pattern for streaming data asynchronously.
/// It's like IEnumerable&lt;T&gt; but each item is retrieved asynchronously.
/// Perfect for continuous data streams like telemetry, file processing, or database queries.
/// </summary>
public interface IObdPoller
{
    /// <summary>
    /// Streams telemetry data continuously from the OBD adapter.
    /// Returns IAsyncEnumerable which can be consumed with "await foreach" syntax.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the stream gracefully</param>
    /// <returns>Continuous stream of telemetry data</returns>
    IAsyncEnumerable<Telemetry> StreamAsync(CancellationToken ct);
}

/// <summary>
/// ObdPoller orchestrates continuous data collection from an OBD adapter.
/// This demonstrates several key architectural patterns:
/// 
/// KEY ARCHITECTURAL CONCEPTS:
/// 
/// 1. **Dependency Injection**: Takes IObdAdapter through constructor
///    - Follows Dependency Inversion Principle
///    - Can work with MockObdAdapter (development) or real ELM327 adapter (production)
///    - Makes unit testing easy with mock adapters
/// 
/// 2. **Async Enumerable Pattern**: Provides streaming data using IAsyncEnumerable
///    - Enables "await foreach" consumption pattern
///    - Memory efficient - doesn't load all data into memory
///    - Backpressure aware - consumers control the pace
/// 
/// 3. **Composition Over Inheritance**: Uses IObdAdapter rather than extending a base class
///    - More flexible than inheritance hierarchies
///    - Easier to test and maintain
///    - Can combine different adapters easily
/// 
/// 4. **Separation of Concerns**: 
///    - This class handles timing and streaming logic
///    - IObdAdapter handles hardware communication
///    - Telemetry class handles data structure
/// 
/// 5. **Configurable Behavior**: Polling period can be customized
///    - Default 10 Hz (100ms) balances responsiveness vs. performance
///    - Can be adjusted based on use case (racing = faster, economy = slower)
/// </summary>
public sealed class ObdPoller : IObdPoller
{
    // ===== DEPENDENCY INJECTION - CONSTRUCTOR DEPENDENCIES =====
    private readonly IObdAdapter _obd;     // The hardware interface (injected dependency)
    private readonly TimeSpan _period;     // How often to poll for new data

    /// <summary>
    /// Constructor demonstrates **Dependency Injection** and **Configuration Pattern**.
    /// 
    /// DEPENDENCY INJECTION BENEFITS:
    /// 1. **Flexibility**: Can use MockObdAdapter or real ELM327 adapter
    /// 2. **Testability**: Easy to inject mock adapters for unit tests
    /// 3. **Loose Coupling**: Doesn't depend on specific adapter implementations
    /// 4. **Single Responsibility**: This class focuses on polling logic, not hardware details
    /// 
    /// CONFIGURATION PATTERN:
    /// - Optional period parameter with sensible default
    /// - Allows customization without breaking existing code
    /// - Default 10 Hz (100ms) is good balance of responsiveness vs. CPU usage
    /// </summary>
    /// <param name="obd">OBD adapter interface (injected by DI container)</param>
    /// <param name="period">Optional polling interval (defaults to 100ms = 10 Hz)</param>
    public ObdPoller(IObdAdapter obd, TimeSpan? period = null)
    {
        _obd = obd;
        _period = period ?? TimeSpan.FromMilliseconds(100); // Default 10 Hz polling rate
    }

    /// <summary>
    /// Streams telemetry data continuously using **Async Enumerable Pattern**.
    /// This is the core of the streaming architecture.
    /// 
    /// ASYNC ENUMERABLE CONCEPTS:
    /// 1. **yield return**: Returns one item at a time without collecting all data in memory
    /// 2. **await foreach**: Consumers can iterate over results asynchronously
    /// 3. **Cancellation Support**: CancellationToken allows graceful shutdown
    /// 4. **Memory Efficient**: Only one telemetry reading in memory at a time
    /// 5. **Backpressure**: Consumer controls pace - if consumer is slow, producer waits
    /// 
    /// DATA FLOW:
    /// Hardware → IObdAdapter → ObdPoller → AgentService → RelayPublisher → Web API
    ///                          ↓
    ///                     MainViewModel → UI Gauges
    /// 
    /// ASYNC ENUMERABLE USAGE:
    /// ```csharp
    /// await foreach (var telemetry in poller.StreamAsync(cancellationToken))
    /// {
    ///     // Process each telemetry reading as it arrives
    ///     UpdateUI(telemetry);
    ///     await SendToServer(telemetry);
    /// }
    /// ```
    /// </summary>
    /// <param name="ct">Cancellation token to stop streaming gracefully</param>
    /// <returns>Continuous stream of telemetry data</returns>
    public async IAsyncEnumerable<Telemetry> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // ===== INITIALIZE CONNECTION =====
        // Connect to the OBD adapter before starting to poll
        // This might involve opening serial port, establishing Bluetooth connection, etc.
        await _obd.ConnectAsync(ct);
        
        // ===== CONTINUOUS POLLING LOOP =====
        // This loop runs until cancellation is requested (app shutdown, user stops, etc.)
        while (!ct.IsCancellationRequested)
        {
            // ===== COLLECT TELEMETRY DATA =====
            // Query all the OBD parameters we're interested in
            // Each call is async because hardware communication takes time
            var t = new Telemetry(
                await _obd.ReadRpmAsync(ct),        // Engine RPM
                await _obd.ReadSpeedKmhAsync(ct),   // Vehicle speed in km/h
                await _obd.ReadCoolantCAsync(ct),   // Coolant temperature in Celsius
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()  // Timestamp when data was collected
            );
            
            // ===== YIELD RETURN - ASYNC ENUMERABLE MAGIC =====
            // This returns the telemetry data to the consumer immediately
            // The method pauses here until the consumer is ready for the next item
            // This provides natural backpressure - if consumer is slow, we slow down
            yield return t;
            
            // ===== RATE LIMITING =====
            // Wait for the specified period before collecting next reading
            // This controls the polling frequency (default 10 Hz = 100ms delay)
            // Prevents overwhelming the OBD adapter or consuming too much CPU
            await Task.Delay(_period, ct);
        }
        
        // When cancellation is requested, the loop exits and the enumerable completes
        // The OBD adapter should implement IDisposable for cleanup (connection close, etc.)
    }
}
