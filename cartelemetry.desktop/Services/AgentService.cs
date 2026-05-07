using System;
using System.Threading;
using System.Threading.Tasks;

using CarTelemetry.Core;
using CarTelemetry.Core.Obd;

using Microsoft.Extensions.Logging;

namespace CarTelemetry.Desktop.Services;

/// <summary>
/// Controls background telemetry forwarding from the desktop dashboard.
/// </summary>
public interface IAgentService
{
    bool IsTransmitting { get; }
    
    Task StartTransmissionAsync();
    
    Task StopTransmissionAsync();
    
    event EventHandler<bool> TransmissionStateChanged;
}

public sealed class AgentService : IAgentService, IDisposable
{
    private readonly IObdPoller _poller;
    private readonly ITelemetryPublisher _publisher;
    private readonly ILogger<AgentService> _logger;
    
    private CancellationTokenSource? _transmissionCts;
    private Task? _transmissionTask;
    private bool _isTransmitting;

    public bool IsTransmitting 
    { 
        get => _isTransmitting;
        private set
        {
            if (_isTransmitting != value)
            {
                _isTransmitting = value;
                TransmissionStateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<bool>? TransmissionStateChanged;

    public AgentService(IObdPoller poller, ITelemetryPublisher publisher, ILogger<AgentService> logger)
    {
        _poller = poller;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task StartTransmissionAsync()
    {
        if (IsTransmitting)
        {
            _logger.LogWarning("Transmission is already running");
            return;
        }

        _logger.LogInformation("Starting telemetry transmission to relay...");
        
        // Each transmission session gets its own cancellation source so stop/start cycles remain isolated.
        _transmissionCts = new CancellationTokenSource();
        
        // Run the stream on a background task; the UI observes IsTransmitting instead of awaiting the loop.
        _transmissionTask = TransmissionLoopAsync(_transmissionCts.Token);
        
        IsTransmitting = true;
        
        await Task.Delay(100);
    }

    public async Task StopTransmissionAsync()
    {
        if (!IsTransmitting)
        {
            _logger.LogWarning("Transmission is not running");
            return;
        }

        _logger.LogInformation("Stopping telemetry transmission...");
        
        // Cancellation is cooperative; the poller and publisher both receive this token.
        _transmissionCts?.Cancel();
        
        if (_transmissionTask != null)
        {
            try
            {
                await _transmissionTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        
        IsTransmitting = false;
        _logger.LogInformation("Telemetry transmission stopped");
    }

    /// <summary>
    /// Publishes each telemetry sample while isolating individual publish failures.
    /// </summary>
    private async Task TransmissionLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var telemetry in _poller.StreamAsync(cancellationToken))
            {
                try
                {
                    await _publisher.PublishAsync(telemetry, cancellationToken);
                    
                    _logger.LogDebug("Published telemetry: RPM={Rpm}, Speed={Speed}", telemetry.Rpm, telemetry.SpeedKmh);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // A dropped publish should not stop the local dashboard or future telemetry samples.
                    _logger.LogError(ex, "Failed to publish telemetry");
                }
                
                // Keep relay publishing near 10 Hz without forcing the OBD poller itself to own that policy.
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transmission loop cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transmission loop failed");
            throw;
        }
    }

    public void Dispose()
    {
        // Dispose may run during app shutdown, gives the background loop a short chance to exit cleanly.
        _transmissionCts?.Cancel();
        _transmissionTask?.Wait(TimeSpan.FromSeconds(5));
        _transmissionCts?.Dispose();
    }
}

