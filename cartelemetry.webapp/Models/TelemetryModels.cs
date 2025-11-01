using Microsoft.AspNetCore.SignalR;

namespace CarTelemetry.WebApp.Models;

// Envelope schema for telemetry data
public record StreamEnvelope(
    string type,
    string vehicleId,
    string sessionId,
    int    v,
    long   ts,
    object payload);

// Telemetry payload structure
public record TelemetryPayload(double? rpm, double? speedKmh, double? coolantC);

// Future: GPS tracking payload for racing lines
public record GpsPayload(double lat, double lng, double altitude, double heading, double speed);

// Future: Motion sensor payload for G-forces
public record MotionPayload(double accelX, double accelY, double accelZ, double gyroX, double gyroY, double gyroZ);

// Future: Engine diagnostics payload
public record EnginePayload(double throttlePosition, double fuelLevel, double oilPressure, double intakeAirTemp);

// SignalR hub for real-time telemetry streaming
public class TelemetryHub : Hub
{
    public override Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()!.Request.Query;
        var vehicleId = query["vehicleId"].ToString();
        var sessionId = query["sessionId"].ToString();
        var group = $"{vehicleId}:{sessionId}";
        
        Console.WriteLine($"Client connected to group: {group}");
        return Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }
}