using CarTelemetry.Core;
using CarTelemetry.Core.Obd;
using CarTelemetry.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {

        // Use the mock adapter by default; swap to Elm327Adapter when running against hardware.
        services.AddSingleton<IObdAdapter, MockObdAdapter>();
        services.AddSingleton<IObdPoller, ObdPoller>();

        var baseUrl   = new Uri("http://localhost:5000");
        var vehicleId = "Z33-01";
        var sessionId = "dev-local";
        var ingestKey = "super-secret-123";

        services.AddSingleton<ITelemetryPublisher>(_ =>
            new RelayPublisher(baseUrl, vehicleId, sessionId, ingestKey));

        services.AddHostedService<TelemetryPumpService>();
    })
    .Build()
    .Run();

