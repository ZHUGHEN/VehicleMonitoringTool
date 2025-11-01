using CarTelemetry.Core;
using CarTelemetry.Core.Obd;
using CarTelemetry.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // For Hardware Implementation
        // ---- ELM327 serial adapter (Windows COMx, Linux /dev/ttyUSB0) ----
        // Windows example: "COM3"; Linux/RPi example: "/dev/ttyUSB0"
        // const string PortName = "COM3"; // <-- change for your machine
        // const int    Baud     = 38400;

        // TODO: swap to Elm327SerialAdapter when hardware is ready
        services.AddSingleton<IObdAdapter, MockObdAdapter>();   // services.AddSingleton<IObdAdapter>(_ => new Elm327SerialAdapter(PortName, Baud));
        services.AddSingleton<IObdPoller, ObdPoller>();

        // Relay config
        var baseUrl   = new Uri("http://localhost:5000"); // switch to HTTPS domain later
        var vehicleId = "Z33-01";
        var sessionId = "dev-local";
        var ingestKey = "super-secret-123";

        services.AddSingleton<ITelemetryPublisher>(_ =>
            new RelayPublisher(baseUrl, vehicleId, sessionId, ingestKey));

        services.AddHostedService<TelemetryPumpService>();
    })
    .Build()
    .Run();
