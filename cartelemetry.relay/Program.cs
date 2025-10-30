using Microsoft.AspNetCore.SignalR;
using CarTelemetry.Relay; // <-- so top-level code can see the types below

var builder = WebApplication.CreateBuilder(args);

// Force a predictable port
builder.WebHost.UseUrls("http://localhost:5000");

// CORS: allow your viewer's origin(s)
builder.Services.AddCors(options =>
{
    options.AddPolicy("viewer", policy => policy
        .WithOrigins(
            "http://localhost:5051",
            "http://127.0.0.1:5051"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors("viewer");
app.UseDefaultFiles();
app.UseStaticFiles();

// Ingest settings
const string IngestHeader = "X-Ingest-Key";
const string IngestKey    = "super-secret-123"; // TODO: move to env var

app.MapPost("/ingest/{vehicleId}/{sessionId}", async (
    HttpRequest req,
    string vehicleId,
    string sessionId,
    StreamEnvelope env,                  // <-- now resolves
    IHubContext<TelemetryHub> hub) =>    // <-- now resolves
{
    if (!req.Headers.TryGetValue(IngestHeader, out var provided) || provided != IngestKey)
        return Results.Unauthorized();

    env = env with { vehicleId = vehicleId, sessionId = sessionId };

    var group = $"{vehicleId}:{sessionId}";
    Console.WriteLine($"INGEST {group} type={env.type} ts={env.ts}");
    await hub.Clients.Group(group).SendAsync("stream", env);
    return Results.Accepted();
});

app.MapHub<TelemetryHub>("/ws");

app.Run();


// ===== Types go in a BLOCK namespace (not file-scoped) =====
namespace CarTelemetry.Relay
{
    // Envelope schema
    public record StreamEnvelope(
        string type,
        string vehicleId,
        string sessionId,
        int    v,
        long   ts,
        object payload);

    // Example payload
    public record TelemetryPayload(double? rpm, double? speedKmh, double? coolantC);

    // SignalR hub
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
