// Threading imports for async operations
using System.Threading;         // For CancellationToken
using System.Threading.Tasks;   // For Task and async/await

namespace CarTelemetry.Core.Obd;

/// <summary>
/// IObdAdapter defines the contract for OBD-II hardware communication.
/// This interface demonstrates **SOLID Principles** and **Dependency Injection** patterns.
/// 
/// KEY DEPENDENCY INJECTION CONCEPTS:
/// 
/// 1. **Dependency Inversion Principle** (the "D" in SOLID):
///    - High-level modules (ObdPoller, DtcService) depend on this abstraction
///    - Low-level modules (MockObdAdapter, Elm327SerialAdapter) implement this abstraction
///    - Allows swapping implementations without changing consuming code
/// 
/// 2. **Interface Segregation Principle** (the "I" in SOLID):
///    - Focused interface with only OBD-related methods
///    - Clients only depend on methods they actually use
///    - Keeps interfaces clean and maintainable
/// 
/// 3. **Liskov Substitution Principle** (the "L" in SOLID):
///    - Any implementation of IObdAdapter should work interchangeably
///    - MockObdAdapter and real ELM327 adapter behave the same from caller's perspective
/// 
/// IMPLEMENTATION STRATEGIES:
/// - MockObdAdapter: Simulated data for development/testing
/// - Elm327SerialAdapter: Real hardware communication via serial port
/// - Elm327BluetoothAdapter: Real hardware via Bluetooth
/// - FileObdAdapter: Replay recorded data from files
/// 
/// ASYNC/AWAIT PATTERNS:
/// - All methods are async because hardware I/O takes time
/// - CancellationToken support for graceful shutdown
/// - Nullable return types (double?) handle missing/invalid sensor data
/// 
/// RESOURCE MANAGEMENT:
/// - Extends IAsyncDisposable for proper cleanup
/// - Ensures connections are closed, ports are released, etc.
/// </summary>
public interface IObdAdapter : IAsyncDisposable
{
    /// <summary>
    /// Establishes connection to the OBD-II adapter hardware.
    /// Implementation varies by adapter type:
    /// - Serial: Open COM port, configure baud rate, send initialization commands
    /// - Bluetooth: Pair device, establish connection, negotiate protocols
    /// - Mock: Immediate success (no real hardware)
    /// </summary>
    /// <param name="ct">Cancellation token for timeout/cancellation</param>
    Task ConnectAsync(CancellationToken ct);
    
    /// <summary>
    /// Reads engine RPM (Revolutions Per Minute) from OBD-II PID 010C.
    /// Returns null if sensor is not available or reading fails.
    /// This is one of the most commonly supported OBD-II parameters.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Engine RPM or null if unavailable</returns>
    Task<double?> ReadRpmAsync(CancellationToken ct);
    
    /// <summary>
    /// Reads vehicle speed in km/h from OBD-II PID 010D.
    /// Returns null if sensor is not available or reading fails.
    /// Speed is typically calculated from wheel sensors or transmission output.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Vehicle speed in km/h or null if unavailable</returns>
    Task<double?> ReadSpeedKmhAsync(CancellationToken ct);
    
    /// <summary>
    /// Reads engine coolant temperature in Celsius from OBD-II PID 0105.
    /// Returns null if sensor is not available or reading fails.
    /// Critical parameter for engine health monitoring - overheating detection.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Coolant temperature in Celsius or null if unavailable</returns>
    Task<double?> ReadCoolantCAsync(CancellationToken ct);

    /// <summary>
    /// Sends raw OBD-II commands directly to the adapter.
    /// Used for advanced operations like diagnostic trouble codes (DTCs).
    /// 
    /// Common raw commands:
    /// - "03": Read stored DTCs
    /// - "04": Clear DTCs and MIL (Check Engine Light)
    /// - "07": Read pending DTCs
    /// - "0A": Read permanent DTCs
    /// - "010C": Read RPM (alternative to ReadRpmAsync)
    /// 
    /// Response format depends on the command and adapter type.
    /// Typically returns hex-encoded data that needs parsing.
    /// </summary>
    /// <param name="command">Raw OBD-II command (e.g., "03", "04", "010C")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Raw response from OBD adapter as string</returns>
    Task<string> SendRawAsync(string command, CancellationToken ct);
}
