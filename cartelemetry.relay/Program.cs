using Microsoft.AspNetCore.SignalR;
using CarTelemetry.Relay;
using CarTelemetry.Relay.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");

var ingestConfig = new IngestConfiguration();
builder.Configuration.GetSection("IngestKeys").Bind(ingestConfig.IngestKeys);

var corsConfig = new CorsConfiguration();
builder.Configuration.GetSection("Cors").Bind(corsConfig);

builder.Services.AddCors(options =>
{
    options.AddPolicy("viewer", policy => policy
        .WithOrigins(corsConfig.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSignalR();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var duration = DateTime.UtcNow - start;
    
    if (context.Request.Path.StartsWithSegments("/ingest"))
    {
        app.Logger.LogInformation("Ingest request: {Method} {Path} -> {StatusCode} ({Duration}ms)",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            duration.TotalMilliseconds);
    }
});

app.UseCors("viewer");
app.UseDefaultFiles();
app.UseStaticFiles();

const string IngestHeader = "X-Ingest-Key";

// Authenticated telemetry ingest endpoint. Valid samples are forwarded to matching SignalR groups.
app.MapPost("/ingest/{vehicleId}/{sessionId}", async (
    HttpRequest req,
    string vehicleId,
    string sessionId,
    StreamEnvelope env,
    IHubContext<TelemetryHub> hub) =>
{
    if (!req.Headers.TryGetValue(IngestHeader, out var providedKey))
    {
        app.Logger.LogWarning("Ingest attempt without key from {RemoteIp}", req.HttpContext.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    var isValidKey = ingestConfig.IngestKeys.Values.Contains(providedKey.ToString());
    if (!isValidKey)
    {
        app.Logger.LogWarning("Invalid ingest key '{Key}' from {RemoteIp}", providedKey, req.HttpContext.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    var keyType = ingestConfig.IngestKeys.FirstOrDefault(kvp => kvp.Value == providedKey).Key ?? "Unknown";
    app.Logger.LogInformation("Valid ingest from {KeyType} key: {VehicleId}:{SessionId}", keyType, vehicleId, sessionId);

    env = env with { vehicleId = vehicleId, sessionId = sessionId };

    var group = $"{vehicleId}:{sessionId}";
    Console.WriteLine($"INGEST {group} type={env.type} ts={env.ts}");
    await hub.Clients.Group(group).SendAsync("stream", env);
    return Results.Accepted();
});

app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    ConfiguredKeys = ingestConfig.IngestKeys.Keys,
    AllowedOrigins = corsConfig.AllowedOrigins
});

app.MapPost("/validate-key", (HttpRequest req) =>
{
    if (!req.Headers.TryGetValue(IngestHeader, out var providedKey))
        return Results.BadRequest("Missing X-Ingest-Key header");

    var isValid = ingestConfig.IngestKeys.Values.Contains(providedKey.ToString());
    var keyType = isValid ? ingestConfig.IngestKeys.FirstOrDefault(kvp => kvp.Value == providedKey).Key : null;
    
    return Results.Ok(new { 
        IsValid = isValid, 
        KeyType = keyType,
        Message = isValid ? "Key is valid" : "Invalid key"
    });
});

app.MapHub<TelemetryHub>("/ws");

app.Run();


namespace CarTelemetry.Relay
{
    public record StreamEnvelope(
        string type,
        string vehicleId,
        string sessionId,
        int    v,
        long   ts,
        object payload);

    public record TelemetryPayload(double? rpm, double? speedKmh, double? coolantC);

    public class TelemetryHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            var q = Context.GetHttpContext()!.Request.Query;
            var vehicleId = q["vehicleId"].ToString();
            var sessionId = q["sessionId"].ToString();
            var group = $"{vehicleId}:{sessionId}";
            return Groups.AddToGroupAsync(Context.ConnectionId, group);
        }
    }
}

