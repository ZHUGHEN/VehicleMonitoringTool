// System imports for threading, async operations, and lifecycle management
using System;
using System.Threading;          // For CancellationToken and CancellationTokenSource (graceful shutdown)
using System.Threading.Tasks;    // For async/await operations

// Core business logic imports
using CarTelemetry.Core;         // Core telemetry interfaces and data models
using CarTelemetry.Core.Obd;     // OBD-II specific interfaces (IObdPoller)

// Logging framework import
using Microsoft.Extensions.Logging; // For structured logging with different levels

namespace CarTelemetry.Desktop.Services;

/// <summary>
/// Interface for the Agent Service that manages telemetry transmission.
/// This follows the Interface Segregation Principle - clients only depend on methods they need.
/// The interface is also used for dependency injection and makes testing easier with mocks.
/// </summary>
public interface IAgentService
{
    /// <summary>Current transmission state - are we actively sending data to the relay?</summary>
    bool IsTransmitting { get; }
    
    /// <summary>Start sending telemetry data to the relay server</summary>
    Task StartTransmissionAsync();
    
    /// <summary>Stop sending telemetry data to the relay server</summary>
    Task StopTransmissionAsync();
    
    /// <summary>Event fired when transmission state changes (started/stopped)</summary>
    event EventHandler<bool> TransmissionStateChanged;
}

/// <summary>
/// AgentService is responsible for managing the telemetry transmission pipeline.
/// This service acts as a bridge between the local OBD data collection and the remote relay server.
/// 
/// Key Responsibilities:
/// 1. Controlling when telemetry transmission starts and stops
/// 2. Running a continuous background loop that streams OBD data to the relay
/// 3. Managing the lifecycle of the transmission task (start, stop, cleanup)
/// 4. Providing status updates to the UI about transmission state
/// 5. Handling errors gracefully and providing proper logging
/// 
/// Architecture Pattern:
/// This implements the "Agent" pattern where this service acts as an intelligent intermediary
/// that can be controlled (start/stop) and provides feedback about its operations.
/// 
/// Threading Model:
/// Uses async/await with CancellationToken for proper background task management.
/// The transmission runs on a background thread and can be safely started/stopped from the UI thread.
/// </summary>
public sealed class AgentService : IAgentService, IDisposable
{
    // ===== DEPENDENCY INJECTION - SERVICES =====
    private readonly IObdPoller _poller;           // Source of telemetry data (from car or mock)
    private readonly ITelemetryPublisher _publisher; // Destination for telemetry data (relay server)
    private readonly ILogger<AgentService> _logger;  // Structured logging for debugging and monitoring
    
    // ===== BACKGROUND TASK MANAGEMENT =====
    private CancellationTokenSource? _transmissionCts; // Used to signal the background task to stop
    private Task? _transmissionTask;                   // The actual background task doing the transmission
    private bool _isTransmitting;                      // Current state flag

    /// <summary>
    /// Current transmission state with automatic event notification.
    /// When this changes, it automatically fires the TransmissionStateChanged event
    /// so subscribers (like MainViewModel) can update their UI accordingly.
    /// </summary>
    public bool IsTransmitting 
    { 
        get => _isTransmitting;
        private set
        {
            if (_isTransmitting != value) // Only fire event if value actually changed
            {
                _isTransmitting = value;
                TransmissionStateChanged?.Invoke(this, value); // Notify all subscribers
            }
        }
    }

    /// <summary>
    /// Event that fires when transmission state changes.
    /// MainViewModel subscribes to this to update the IsTransmitting property and UI button states.
    /// The bool parameter indicates the new transmission state (true = started, false = stopped).
    /// </summary>
    public event EventHandler<bool>? TransmissionStateChanged;

    /// <summary>
    /// Constructor called by dependency injection system.
    /// All dependencies are provided by the DI container configured in Program.cs.
    /// </summary>
    /// <param name="poller">OBD data source (MockObdAdapter in development, real ELM327 in production)</param>
    /// <param name="publisher">Relay destination (RelayPublisher configured with server URL and credentials)</param>
    /// <param name="logger">Logging service for debugging and monitoring</param>
    public AgentService(IObdPoller poller, ITelemetryPublisher publisher, ILogger<AgentService> logger)
    {
        _poller = poller;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Starts the telemetry transmission to the relay server.
    /// This creates and starts a background task that continuously streams OBD data to the relay.
    /// 
    /// Process:
    /// 1. Check if already transmitting (prevent duplicate tasks)
    /// 2. Create a new CancellationTokenSource for controlling the background task
    /// 3. Start the background transmission loop task
    /// 4. Update state and notify subscribers
    /// 5. Brief delay to ensure task starts properly
    /// 
    /// This method is async but returns quickly - the actual transmission happens on a background thread.
    /// </summary>
    public async Task StartTransmissionAsync()
    {
        if (IsTransmitting)
        {
            _logger.LogWarning("Transmission is already running");
            return;
        }

        _logger.LogInformation("Starting telemetry transmission to relay...");
        
        // Create cancellation token for controlling the background task
        _transmissionCts = new CancellationTokenSource();
        
        // Start the background transmission loop (doesn't block this method)
        _transmissionTask = TransmissionLoopAsync(_transmissionCts.Token);
        
        // Update state (this triggers the TransmissionStateChanged event)
        IsTransmitting = true;
        
        // Give the background task a moment to start before returning
        // This ensures the task is properly running before we tell the caller we've started
        await Task.Delay(100);
    }

    /// <summary>
    /// Stops the telemetry transmission to the relay server.
    /// This gracefully shuts down the background transmission task and cleans up resources.
    /// 
    /// Process:
    /// 1. Check if actually transmitting (can't stop what's not running)
    /// 2. Cancel the background task using the CancellationToken
    /// 3. Wait for the task to finish (with proper exception handling)
    /// 4. Update state and notify subscribers
    /// 5. Log the completion
    /// 
    /// This method waits for the background task to properly shut down, ensuring clean resource cleanup.
    /// </summary>
    public async Task StopTransmissionAsync()
    {
        if (!IsTransmitting)
        {
            _logger.LogWarning("Transmission is not running");
            return;
        }

        _logger.LogInformation("Stopping telemetry transmission...");
        
        // Signal the background task to cancel
        _transmissionCts?.Cancel();
        
        // Wait for the background task to finish cleanly
        if (_transmissionTask != null)
        {
            try
            {
                await _transmissionTask; // Wait for graceful shutdown
            }
            catch (OperationCanceledException)
            {
                // This is expected when we cancel the task - not an error
            }
        }
        
        // Update state (this triggers the TransmissionStateChanged event)
        IsTransmitting = false;
        _logger.LogInformation("Telemetry transmission stopped");
    }

    /// <summary>
    /// The core transmission loop that runs on a background thread.
    /// This method continuously streams telemetry data from the OBD poller to the relay publisher.
    /// 
    /// Process:
    /// 1. Stream telemetry data from the OBD poller (async enumerable)
    /// 2. For each telemetry reading:
    ///    a. Publish it to the relay server
    ///    b. Log success/failure
    ///    c. Wait 100ms (creates ~10 Hz transmission rate)
    /// 3. Handle cancellation gracefully when stop is requested
    /// 4. Handle other errors with proper logging
    /// 
    /// Key Design Points:
    /// - Uses async enumerable to stream data efficiently
    /// - Publishes each reading individually (real-time streaming)
    /// - 10 Hz rate balances responsiveness with network efficiency
    /// - Distinguishes between cancellation (expected) and errors (unexpected)
    /// - Continues on publication errors (network hiccups shouldn't stop the loop)
    /// </summary>
    /// <param name="cancellationToken">Token to signal when to stop the loop</param>
    private async Task TransmissionLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Stream telemetry data continuously from the OBD poller
            // This is an async enumerable - each iteration gets the next reading
            await foreach (var telemetry in _poller.StreamAsync(cancellationToken))
            {
                try
                {
                    // Send this telemetry reading to the relay server
                    await _publisher.PublishAsync(telemetry, cancellationToken);
                    
                    // Log successful transmission (Debug level - only shows when debugging)
                    _logger.LogDebug("Published telemetry: RPM={Rpm}, Speed={Speed}", telemetry.Rpm, telemetry.SpeedKmh);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // Log publication errors but continue the loop
                    // Network issues, server downtime, etc. shouldn't stop data collection
                    _logger.LogError(ex, "Failed to publish telemetry");
                }
                
                // Control transmission rate: 100ms delay = ~10 Hz
                // This balances real-time responsiveness with network efficiency
                // Too fast: overwhelms network/server, Too slow: data feels stale
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when StopTransmissionAsync() is called
            _logger.LogInformation("Transmission loop cancelled");
            throw; // Re-throw so the caller knows it was cancelled
        }
        catch (Exception ex)
        {
            // Unexpected errors in the transmission loop
            _logger.LogError(ex, "Transmission loop failed");
            throw; // Re-throw so the caller knows something went wrong
        }
    }

    /// <summary>
    /// IDisposable implementation for proper resource cleanup.
    /// This is called automatically when the service is disposed (app shutdown, DI container cleanup).
    /// 
    /// Cleanup Process:
    /// 1. Cancel any running transmission task
    /// 2. Wait up to 5 seconds for graceful shutdown
    /// 3. Dispose the CancellationTokenSource
    /// 
    /// This ensures no background tasks are left running when the application shuts down.
    /// </summary>
    public void Dispose()
    {
        _transmissionCts?.Cancel();                      // Signal background task to stop
        _transmissionTask?.Wait(TimeSpan.FromSeconds(5)); // Wait for graceful shutdown (max 5 seconds)
        _transmissionCts?.Dispose();                     // Clean up the cancellation token source
    }
}