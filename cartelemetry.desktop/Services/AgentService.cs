using System;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core;
using CarTelemetry.Core.Obd;
using Microsoft.Extensions.Logging;

namespace CarTelemetry.Desktop.Services;

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
        
        _transmissionCts = new CancellationTokenSource();
        _transmissionTask = TransmissionLoopAsync(_transmissionCts.Token);
        
        IsTransmitting = true;
        
        // Give the task a moment to start
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
        
        _transmissionCts?.Cancel();
        
        if (_transmissionTask != null)
        {
            try
            {
                await _transmissionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
        
        IsTransmitting = false;
        _logger.LogInformation("Telemetry transmission stopped");
    }

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
                    _logger.LogError(ex, "Failed to publish telemetry");
                }
                
                // ~10 Hz transmission rate
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
        _transmissionCts?.Cancel();
        _transmissionTask?.Wait(TimeSpan.FromSeconds(5));
        _transmissionCts?.Dispose();
    }
}