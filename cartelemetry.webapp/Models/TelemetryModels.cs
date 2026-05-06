using Microsoft.AspNetCore.SignalR;

namespace CarTelemetry.WebApp.Models;

/// <summary>
/// Versioned SignalR envelope shared by telemetry producers and viewers.
/// </summary>
public record StreamEnvelope(
    string type,
    string vehicleId,
    string sessionId,
    int    v,
    long   ts,
    object payload);

public record TelemetryPayload(double? rpm, double? speedKmh, double? coolantC);

public record GpsPayload(double lat, double lng, double altitude, double heading, double speed);

public record MotionPayload(double accelX, double accelY, double accelZ, double gyroX, double gyroY, double gyroZ);

public record EnginePayload(double throttlePosition, double fuelLevel, double oilPressure, double intakeAirTemp);

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

