using Microsoft.AspNetCore.SignalR;
using CarTelemetry.WebApp.Models;
using CarTelemetry.WebApp.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure ports
builder.WebHost.UseUrls("http://localhost:5052", "https://localhost:7052");

// Add services to the container.
builder.Services.AddRazorPages();

// Load configuration
var ingestConfig = new IngestConfiguration();
builder.Configuration.GetSection("IngestKeys").Bind(ingestConfig.IngestKeys);

var corsConfig = new CorsConfiguration();
builder.Configuration.GetSection("Cors").Bind(corsConfig);

// CORS: allow configured origins (for API access from other domains)
builder.Services.AddCors(options =>
{
    options.AddPolicy("telemetryApi", policy => policy
        .WithOrigins(corsConfig.AllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// Request logging middleware for telemetry ingest
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

app.UseCors("telemetryApi");
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// === TELEMETRY RELAY ENDPOINTS ===
const string IngestHeader = "X-Ingest-Key";

// Main telemetry ingest endpoint
app.MapPost("/ingest/{vehicleId}/{sessionId}", async (
    HttpRequest req,
    string vehicleId,
    string sessionId,
    StreamEnvelope env,
    IHubContext<TelemetryHub> hub) =>
{
    // Validate ingest key
    if (!req.Headers.TryGetValue(IngestHeader, out var providedKey))
    {
        app.Logger.LogWarning("Ingest attempt without key from {RemoteIp}", req.HttpContext.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    // Check if the provided key matches any of our configured keys
    var isValidKey = ingestConfig.IngestKeys.Values.Contains(providedKey.ToString());
    if (!isValidKey)
    {
        app.Logger.LogWarning("Invalid ingest key '{Key}' from {RemoteIp}", providedKey, req.HttpContext.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    // Log successful ingest
    var keyType = ingestConfig.IngestKeys.FirstOrDefault(kvp => kvp.Value == providedKey).Key ?? "Unknown";
    app.Logger.LogInformation("Valid ingest from {KeyType} key: {VehicleId}:{SessionId}", keyType, vehicleId, sessionId);

    // Ensure vehicleId and sessionId are set in envelope
    env = env with { vehicleId = vehicleId, sessionId = sessionId };

    var group = $"{vehicleId}:{sessionId}";
    Console.WriteLine($"INGEST {group} type={env.type} ts={env.ts}");
    await hub.Clients.Group(group).SendAsync("stream", env);
    return Results.Accepted();
});

// Health check endpoint
app.MapGet("/api/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    ConfiguredKeys = ingestConfig.IngestKeys.Keys,
    AllowedOrigins = corsConfig.AllowedOrigins
});

// Key validation endpoint (for testing)
app.MapPost("/api/validate-key", (HttpRequest req) =>
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

// SignalR hub for real-time telemetry streaming
app.MapHub<TelemetryHub>("/telemetryHub");

app.Run();
