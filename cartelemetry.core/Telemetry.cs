namespace CarTelemetry.Core;

/// <summary>
/// Telemetry record represents a single snapshot of vehicle data at a point in time.
/// This demonstrates **Data Transfer Object (DTO)** pattern and modern C# record types.
/// 
/// KEY ARCHITECTURAL CONCEPTS:
/// 
/// 1. **Immutable Data Structure**: 
///    - Record types are immutable by default
///    - Once created, values cannot be changed
///    - Prevents accidental data corruption
///    - Thread-safe by design
/// 
/// 2. **Value Semantics**: 
///    - Records provide value-based equality (not reference-based)
///    - Two Telemetry objects with same data are considered equal
///    - Useful for testing, caching, and comparison operations
/// 
/// 3. **Data Transfer Object (DTO) Pattern**: 
///    - Simple data container with no business logic
///    - Used to transfer data between layers/services
///    - Serializes cleanly to JSON for API calls
/// 
/// 4. **Nullable Design**: 
///    - double? allows for missing/unavailable sensor data
///    - Not all OBD-II parameters are available on all vehicles
///    - Graceful handling of sensor failures or unsupported features
/// 
/// 5. **Cross-Layer Communication**: 
///    - Used throughout the entire application stack
///    - Hardware → Core → Services → ViewModels → UI
///    - Single data format reduces mapping and conversion overhead
/// 
/// DATA FLOW THROUGH SYSTEM:
/// 
/// 1. **Creation**: ObdPoller creates from IObdAdapter readings
/// 2. **Processing**: MainViewModel updates UI properties  
/// 3. **Transmission**: AgentService sends via ITelemetryPublisher
/// 4. **Serialization**: RelayPublisher converts to JSON for HTTP API
/// 
/// EXAMPLE USAGE:
/// ```csharp
/// var telemetry = new Telemetry(
///     Rpm: 3500.0,
///     SpeedKmh: 120.5,
///     CoolantC: 85.2,
///     TsUtcMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
/// );
/// ```
/// 
/// JSON SERIALIZATION EXAMPLE:
/// ```json
/// {
///   "rpm": 3500.0,
///   "speedKmh": 120.5, 
///   "coolantC": 85.2,
///   "tsUtcMs": 1699123456789
/// }
/// ```
/// </summary>
/// <param name="Rpm">Engine RPM (Revolutions Per Minute) - null if unavailable</param>
/// <param name="SpeedKmh">Vehicle speed in kilometers per hour - null if unavailable</param>
/// <param name="CoolantC">Engine coolant temperature in Celsius - null if unavailable</param>
/// <param name="TsUtcMs">Timestamp in UTC milliseconds since Unix epoch - when data was collected</param>
public record Telemetry(
    double? Rpm,        // Engine speed - critical for performance monitoring
    double? SpeedKmh,   // Vehicle speed - primary driving parameter  
    double? CoolantC,   // Coolant temperature - engine health indicator
    long TsUtcMs);      // Collection timestamp - for time-series analysis
