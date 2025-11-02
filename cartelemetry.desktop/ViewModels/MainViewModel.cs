// System imports for collections, property change notifications, diagnostics, file I/O, and threading
using System;
using System.Collections.ObjectModel;      // For collections that notify UI when items are added/removed
using System.ComponentModel;               // For INotifyPropertyChanged interface (MVVM property notifications)
using System.Diagnostics;                  // For Stopwatch class (lap timer) and debug output
using System.IO;                          // For file operations (loading/saving gauge configuration)
using System.Linq;                        // For LINQ operations on collections
using System.Runtime.CompilerServices;    // For [CallerMemberName] attribute in property change notifications
using System.Text.Json;                   // For JSON serialization/deserialization of configuration
using System.Threading;                   // For CancellationToken (graceful shutdown)
using System.Threading.Tasks;             // For async/await operations

// Core business logic layer imports
using CarTelemetry.Core;                  // Core telemetry data models and interfaces
using CarTelemetry.Core.Obd;              // OBD-II specific interfaces and services

// Desktop application layer imports
using CarTelemetry.Desktop.Configuration; // Configuration classes for gauge settings
using CarTelemetry.Desktop.Models;        // Data models specific to the desktop UI (like LapTime)
using CarTelemetry.Desktop.Services;      // Services for agent communication and data transmission

// MVVM framework import
using CommunityToolkit.Mvvm.Input;       // For [RelayCommand] attribute to create ICommand properties

// Dependency injection import
using Microsoft.Extensions.DependencyInjection; // For resolving services from DI container

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// MainViewModel is the central hub of the CarTelemetry application and the primary ViewModel in the MVVM pattern.
/// 
/// MVVM Pattern Explanation:
/// - Model: Telemetry data, gauge configurations, lap times (the "what")
/// - View: MainWindow.axaml, gauge controls, UI elements (the "how it looks")  
/// - ViewModel: THIS CLASS - bridges Model and View, contains business logic (the "how it works")
/// 
/// This ViewModel is responsible for:
/// 1. Real-time telemetry data management (RPM, Speed, Coolant temp, etc.)
/// 2. Gauge configuration and display logic
/// 3. Lap timer functionality with best lap tracking
/// 4. UI state management (Settings overlay, Screensaver, Connection status)
/// 5. Data transmission control (Start/Stop sending to relay)
/// 6. Property change notifications to update the UI automatically
/// 
/// Key Design Patterns Used:
/// - MVVM (Model-View-ViewModel) for separation of concerns
/// - Observer Pattern via INotifyPropertyChanged for UI updates
/// - Command Pattern via RelayCommand for user actions
/// - Dependency Injection for loose coupling with services
/// - Async/Await for non-blocking operations
/// </summary>
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    // ===== DEPENDENCY INJECTION - SERVICES =====
    // These services are injected via constructor and provide the core functionality
    private readonly IObdPoller _poller;           // Continuously polls OBD-II data from car/mock adapter
    private readonly IAgentService _agentService;  // Manages telemetry transmission to relay server
    
    // ===== MVVM PROPERTY CHANGE NOTIFICATION =====
    // This event is part of INotifyPropertyChanged interface
    // When a property changes, this event fires and tells the UI to update
    // The UI framework (Avalonia) automatically subscribes to this event for data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    // ===== PRIVATE BACKING FIELDS =====
    // These store the actual data - the public properties expose them with change notifications
    // Using private fields with public properties is the standard MVVM pattern for data binding
    
    // Real-time telemetry data from the car
    private double _rpm;        // Engine RPM (Revolutions Per Minute) - how fast engine is spinning
    private double _speedMph;   // Vehicle speed in Miles Per Hour (converted from km/h)
    private double _coolantC;   // Engine coolant temperature in Celsius - critical for engine health
    
    // Application state flags
    private bool _isConnected = false;      // Are we successfully receiving data from OBD adapter?
    private bool _isTransmitting = false;   // Are we sending data to the relay server?
    private bool _isShowingSettings = false; // Is the settings overlay currently visible?
    private bool _isShowingScreensaver = false; // Is the screensaver currently active?
    
    // Time tracking
    private DateTime _lastUpdateTime = DateTime.Now;  // When did we last receive telemetry data?
    private DateTime _currentTime = DateTime.Now;     // Current system time (for screensaver clock)
    
    // Gauge configuration - determines which gauge types are shown in each of the 6 slots
    private GaugeConfiguration _gaugeConfig = GaugeConfiguration.CreateDefault();

    // ===== LAP TIMER PRIVATE FIELDS =====
    // Stopwatch provides high-precision timing for lap measurements
    private Stopwatch _lapTimer = new();    // System.Diagnostics.Stopwatch for accurate timing
    private TimeSpan _currentLapTime;       // How long has the current lap been running?
    private TimeSpan? _bestLapTime;         // What's the fastest lap time? (nullable - might not have one yet)
    private TimeSpan? _lastLapTime;         // What was the previous lap time? (nullable - might be first lap)
    private int _lapCount = 1;              // What lap number are we on? (starts at 1, increments each lap)

    // ===== PUBLIC PROPERTIES WITH CHANGE NOTIFICATIONS =====
    // These properties automatically notify the UI when their values change
    // The OnChanged() method fires PropertyChanged event, which triggers UI updates
    // This is the core of MVVM data binding - when these change, gauges/displays update automatically
    
    /// <summary>Engine RPM - displayed on RPM gauge and used for calculations</summary>
    public double Rpm { get => _rpm; private set { _rpm = value; OnChanged(); } }
    
    /// <summary>Vehicle speed in MPH - converted from km/h, displayed on speedometer</summary>
    public double SpeedMph { get => _speedMph; private set { _speedMph = value; OnChanged(); } }
    
    /// <summary>Engine coolant temperature in Celsius - critical for engine health monitoring</summary>
    public double CoolantC { get => _coolantC; private set { _coolantC = value; OnChanged(); } }
    
    /// <summary>Connection status - shows green/red indicator in UI</summary>
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnChanged(); } }
    
    /// <summary>Transmission status - shows if we're sending data to relay server</summary>
    public bool IsTransmitting { get => _isTransmitting; private set { _isTransmitting = value; OnChanged(); } }
    
    /// <summary>Settings overlay visibility - controls if settings popup is shown over main dashboard</summary>
    public bool IsShowingSettings { get => _isShowingSettings; private set { _isShowingSettings = value; OnChanged(); } }
    
    /// <summary>Screensaver visibility - controls if screensaver is shown (fullscreen with clock)</summary>
    public bool IsShowingScreensaver { get => _isShowingScreensaver; private set { _isShowingScreensaver = value; OnChanged(); } }
    
    /// <summary>Last telemetry update time - helps identify connection issues</summary>
    public DateTime LastUpdateTime { get => _lastUpdateTime; private set { _lastUpdateTime = value; OnChanged(); } }
    
    /// <summary>Current system time - displayed on screensaver clock</summary>
    public DateTime CurrentTime { get => _currentTime; private set { _currentTime = value; OnChanged(); } }
    
    // ===== LAP TIMER PUBLIC PROPERTIES =====
    /// <summary>Current running lap time - updates in real-time while timer is running</summary>
    public TimeSpan CurrentLapTime { get => _currentLapTime; private set { _currentLapTime = value; OnChanged(); } }
    
    /// <summary>Best (fastest) lap time across all laps - highlighted in UI</summary>
    public TimeSpan? BestLapTime { get => _bestLapTime; private set { _bestLapTime = value; OnChanged(); } }
    
    /// <summary>Previous lap time - helps compare current performance</summary>
    public TimeSpan? LastLapTime { get => _lastLapTime; private set { _lastLapTime = value; OnChanged(); } }
    
    /// <summary>Current lap number - starts at 1, increments with each completed lap</summary>
    public int LapCount { get => _lapCount; private set { _lapCount = value; OnChanged(); } }
    
    /// <summary>
    /// Collection of all completed lap times with metadata.
    /// ObservableCollection automatically notifies UI when items are added/removed,
    /// so the lap times list in the UI updates automatically when new laps are recorded.
    /// </summary>
    public ObservableCollection<LapTime> LapTimes { get; } = new();
    
    // ===== GAUGE CONFIGURATION PROPERTIES =====
    // ===== GAUGE CONFIGURATION PROPERTIES =====
    /// <summary>
    /// Main gauge configuration object that determines which gauge types are displayed in each of the 6 slots.
    /// When this changes, it triggers updates to all individual slot properties and rebuilds the gauge ViewModels.
    /// This configuration is persisted to JSON file so user's gauge choices are remembered between app restarts.
    /// </summary>
    public GaugeConfiguration GaugeConfig 
    { 
        get => _gaugeConfig; 
        private set 
        { 
            _gaugeConfig = value; 
            OnChanged(); // Notify that GaugeConfig itself changed
            
            // Notify that all individual gauge slot properties have changed
            // This ensures the UI updates even when just the internal configuration changes
            OnChanged(nameof(GaugeSlot1));
            OnChanged(nameof(GaugeSlot2));
            OnChanged(nameof(GaugeSlot3));
            OnChanged(nameof(GaugeSlot4));
            OnChanged(nameof(GaugeSlot5));
            OnChanged(nameof(GaugeSlot6));
            
            // Rebuild all gauge ViewModels with the new configuration
            UpdateGaugeViewModels();
        } 
    }
    
    // ===== INDIVIDUAL GAUGE SLOT PROPERTIES =====
    // These provide individual access to each gauge slot for data binding in the UI
    // The Settings view binds to these to show dropdown selectors for each gauge position
    /// <summary>Top-left gauge type (Position 1)</summary>
    public GaugeType GaugeSlot1 => GaugeConfig.GaugeSlot1;
    /// <summary>Top-center gauge type (Position 2)</summary>
    public GaugeType GaugeSlot2 => GaugeConfig.GaugeSlot2;
    /// <summary>Top-right gauge type (Position 3)</summary>
    public GaugeType GaugeSlot3 => GaugeConfig.GaugeSlot3;
    /// <summary>Bottom-left gauge type (Position 4)</summary>
    public GaugeType GaugeSlot4 => GaugeConfig.GaugeSlot4;
    /// <summary>Bottom-center gauge type (Position 5)</summary>
    public GaugeType GaugeSlot5 => GaugeConfig.GaugeSlot5;
    /// <summary>Bottom-right gauge type (Position 6)</summary>
    public GaugeType GaugeSlot6 => GaugeConfig.GaugeSlot6;
    
    // ===== DYNAMIC GAUGE VIEWMODELS =====
    /// <summary>
    /// Collection of 6 GaugeViewModel objects that represent the actual gauge controls in the UI.
    /// Each GaugeViewModel knows how to display a specific gauge type (RPM, Speed, etc.) and 
    /// automatically updates its display value when new telemetry data arrives.
    /// ObservableCollection ensures the UI updates when gauges are added/removed/changed.
    /// </summary>
    public ObservableCollection<GaugeViewModel> Gauges { get; } = new();

    /// <summary>
    /// Rebuilds the Gauges collection based on the current GaugeConfig.
    /// This is called whenever the gauge configuration changes (either from settings or loading from file).
    /// Creates 6 new GaugeViewModel instances, one for each configured gauge type.
    /// </summary>
    private void UpdateGaugeViewModels()
    {
        Gauges.Clear(); // Remove all existing gauge ViewModels
        
        // Create 6 new gauge ViewModels based on current configuration
        // Each GaugeViewModel knows how to display its assigned gauge type
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot1));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot2));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot3));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot4));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot5));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot6));
    }

    // ===== RELAY COMMAND METHODS =====
    // These methods are decorated with [RelayCommand] which automatically creates ICommand properties
    // The MVVM toolkit generates ToggleTransmissionCommand, OpenSettingsCommand, etc. properties
    // These commands are bound to buttons in the UI for user interactions
    
    /// <summary>
    /// Toggles telemetry transmission to the relay server on/off.
    /// Uses the AgentService to start or stop sending telemetry data to the web dashboard.
    /// This is bound to the transmission toggle button in the UI.
    /// </summary>
    [RelayCommand]
    public async Task ToggleTransmissionAsync()
    {
        if (IsTransmitting)
        {
            await _agentService.StopTransmissionAsync();
        }
        else
        {
            await _agentService.StartTransmissionAsync();
        }
    }

    /// <summary>
    /// Opens the settings overlay for gauge configuration.
    /// Sets IsShowingSettings to true, which triggers the settings popup to appear over the main dashboard.
    /// Bound to the settings button in the header.
    /// </summary>
    [RelayCommand]
    public void OpenSettings()
    {
        IsShowingSettings = true;
    }

    /// <summary>
    /// Closes the settings overlay and returns to the main dashboard.
    /// Sets IsShowingSettings to false, which hides the settings popup.
    /// Bound to the back button in the settings overlay.
    /// </summary>
    [RelayCommand]
    public void GoBack()
    {
        IsShowingSettings = false;
    }

    /// <summary>
    /// Activates the screensaver overlay.
    /// Sets IsShowingScreensaver to true, which shows the fullscreen screensaver with Nismo branding and clock.
    /// Bound to the Nismo logo button in the header.
    /// </summary>
    [RelayCommand]
    public void OpenScreensaver()
    {
        IsShowingScreensaver = true;
    }

    /// <summary>
    /// Exits the screensaver and returns to the main dashboard.
    /// Sets IsShowingScreensaver to false, which hides the screensaver overlay.
    /// Bound to touch/click events on the screensaver.
    /// </summary>
    [RelayCommand]
    public void ExitScreensaver()
    {
        IsShowingScreensaver = false;
    }

    // ===== LAP TIMER COMMAND METHODS =====
    
    /// <summary>
    /// Starts the lap timer for a new timing session.
    /// Restarts the Stopwatch and resets the current lap time to zero.
    /// This begins timing for the first lap.
    /// Bound to the Start button in the lap timer interface.
    /// </summary>
    [RelayCommand]
    public void StartTimer()
    {
        _lapTimer.Restart();         // Reset and start the stopwatch
        CurrentLapTime = TimeSpan.Zero; // Reset display to 00:00.000
    }

    /// <summary>
    /// Records a completed lap and starts timing the next lap.
    /// This is the core lap timing functionality that:
    /// 1. Captures the current lap time from the stopwatch
    /// 2. Creates a new LapTime record with lap number and timestamp
    /// 3. Checks if this is a new best lap and updates accordingly
    /// 4. Adds the lap to the LapTimes collection (which updates the UI list)
    /// 5. Increments the lap counter
    /// 6. Restarts the timer for the next lap
    /// Bound to the Lap button in the lap timer interface.
    /// </summary>
    [RelayCommand]
    public void LapTimer()
    {
        if (_lapTimer.IsRunning)
        {
            var lapTime = _lapTimer.Elapsed; // Capture the exact moment the lap button was pressed
            
            // Create new lap record with all the metadata
            var lap = new LapTime
            {
                LapNumber = LapCount,           // What lap number this was (1, 2, 3, etc.)
                Time = lapTime,                 // How long this lap took
                Timestamp = DateTime.Now        // When this lap was completed (for session records)
            };
            
            // Check if this is a new best (fastest) lap
            if (!BestLapTime.HasValue || lapTime < BestLapTime.Value)
            {
                // Mark any previous best lap as no longer being the best
                foreach (var existingLap in LapTimes)
                    existingLap.IsBest = false;
                
                // Mark this new lap as the best and update the overall best time
                lap.IsBest = true;
                BestLapTime = lapTime;
            }
            
            // Update tracking variables
            LastLapTime = lapTime;          // Remember this as the "previous" lap for next time
            LapTimes.Add(lap);              // Add to the collection (automatically updates UI list)
            LapCount++;                     // Increment for the next lap
            
            // Restart timer for next lap (keeps timing continuously)
            _lapTimer.Restart();
            CurrentLapTime = TimeSpan.Zero; // Reset the display
        }
    }

    /// <summary>
    /// Stops the lap timer without recording a lap.
    /// Pauses timing but keeps the current lap time displayed.
    /// Bound to the Stop button in the lap timer interface.
    /// </summary>
    [RelayCommand]
    public void StopTimer()
    {
        _lapTimer.Stop(); // Pause the stopwatch (can be resumed)
    }

    /// <summary>
    /// Completely resets the lap timer session.
    /// Clears all lap times, resets counters, and stops timing.
    /// This starts a fresh timing session.
    /// Bound to the Reset button in the lap timer interface.
    /// </summary>
    [RelayCommand]
    public void ResetTimer()
    {
        _lapTimer.Reset();              // Stop and reset stopwatch to zero
        CurrentLapTime = TimeSpan.Zero; // Reset current lap display
        BestLapTime = null;             // Clear best lap (no laps recorded yet)
        LastLapTime = null;             // Clear last lap (no laps recorded yet)
        LapCount = 1;                   // Reset to lap 1
        LapTimes.Clear();               // Remove all recorded laps from the list
    }

    // ===== CONSTRUCTOR AND INITIALIZATION =====
    /// <summary>
    /// MainViewModel constructor - this is called by the dependency injection system.
    /// Sets up all the necessary subscriptions and starts the main data processing loop.
    /// </summary>
    /// <param name="poller">OBD poller service for receiving telemetry data</param>
    /// <param name="agentService">Agent service for transmission control</param>
    public MainViewModel(IObdPoller poller, IAgentService agentService)
    {
        _poller = poller;           // Store reference to OBD data poller
        _agentService = agentService; // Store reference to transmission agent
        
        // Subscribe to transmission state changes from the agent service
        // When the agent starts/stops transmission, update our IsTransmitting property
        _agentService.TransmissionStateChanged += (s, isTransmitting) =>
        {
            IsTransmitting = isTransmitting;
        };
        
        // Initialize the gauge ViewModels with default configuration
        UpdateGaugeViewModels();
        
        // Load saved gauge configuration from disk (async, doesn't block startup)
        _ = Task.Run(LoadGaugeConfigurationAsync);
        
        // Start the main telemetry processing loop (async, doesn't block startup)
        _ = RunAsync();
    }

    // ===== CONFIGURATION MANAGEMENT =====
    /// <summary>
    /// Loads gauge configuration from JSON file in the user's application data folder.
    /// This preserves user's gauge selections between app restarts.
    /// The configuration is stored in %APPDATA%\CarTelemetry\gauge-config.json on Windows.
    /// </summary>
    private async Task LoadGaugeConfigurationAsync()
    {
        try
        {
            // Get the application data folder path (varies by OS)
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarTelemetry");
            var filePath = Path.Combine(appDataPath, "gauge-config.json");
            
            // If no saved configuration exists, use defaults
            if (!File.Exists(filePath))
            {
                GaugeConfig = GaugeConfiguration.CreateDefault();
                return;
            }

            // Read and deserialize the JSON configuration
            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<GaugeConfiguration>(json);
            
            if (config != null)
            {
                GaugeConfig = config; // This triggers UpdateGaugeViewModels() via the setter
            }
        }
        catch (Exception ex)
        {
            // If anything goes wrong loading config, fall back to defaults and log the error
            System.Diagnostics.Debug.WriteLine($"Failed to load gauge configuration in MainViewModel: {ex.Message}");
            GaugeConfig = GaugeConfiguration.CreateDefault();
        }
    }

    /// <summary>
    /// Refreshes the gauge configuration by reloading from disk.
    /// Called by SettingsViewModel after saving new configuration to ensure MainViewModel picks up changes.
    /// </summary>
    public async Task RefreshGaugeConfigurationAsync()
    {
        await LoadGaugeConfigurationAsync();
    }

    // ===== MAIN TELEMETRY PROCESSING LOOP =====
    /// <summary>
    /// The main telemetry processing loop that runs continuously while the app is open.
    /// This method:
    /// 1. Connects to the OBD poller service
    /// 2. Receives streaming telemetry data (RPM, speed, coolant temp, etc.)
    /// 3. Updates all properties with new data (which triggers UI updates via data binding)
    /// 4. Updates lap timer if it's running
    /// 5. Updates all gauge ViewModels with the latest data
    /// 
    /// This runs asynchronously and doesn't block the UI thread.
    /// </summary>
    private async Task RunAsync()
    {
        using var cts = new CancellationTokenSource(); // For graceful shutdown (not currently used)
        
        try
        {
            IsConnected = true; // Assume connection will succeed
            
            // Stream telemetry data continuously from the OBD poller
            // This is an async enumerable - each iteration gets the next telemetry reading
            await foreach (var t in _poller.StreamAsync(cts.Token))
            {
                // Update main telemetry properties (triggers UI updates via data binding)
                Rpm = t.Rpm ?? 0;                                    // Engine RPM
                SpeedMph = (t.SpeedKmh ?? 0) * 0.621371;             // Convert km/h to mph
                CoolantC = t.CoolantC ?? 0;                          // Coolant temperature
                LastUpdateTime = DateTime.Now;                       // When we got this data
                CurrentTime = DateTime.Now;                          // Current time (for screensaver clock)
                IsConnected = true;                                  // We're successfully receiving data
                
                // Update lap timer display if it's currently running
                if (_lapTimer.IsRunning)
                {
                    CurrentLapTime = _lapTimer.Elapsed; // Show current lap time in real-time
                }
                
                // Update all gauge ViewModels with the latest telemetry data
                // Each gauge knows which value to display based on its configured type
                foreach (var gauge in Gauges)
                {
                    gauge.UpdateValue(
                        Rpm, SpeedMph, CoolantC,    // Real telemetry data
                        engineLoad: 45,             // Mock data for now (would come from OBD in production)
                        fuelPressure: 35,           // Mock data for now
                        fuelTrim: 5);               // Mock data for now
                }
            }
        }
        catch (Exception)
        {
            // If the telemetry stream fails, mark as disconnected
            // This could happen if OBD adapter is unplugged, serial port issues, etc.
            IsConnected = false;
        }
    }

    // ===== PROPERTY CHANGE NOTIFICATION HELPER =====
    /// <summary>
    /// Helper method for implementing INotifyPropertyChanged.
    /// This fires the PropertyChanged event to notify the UI that a property has changed.
    /// The [CallerMemberName] attribute automatically provides the property name,
    /// so you can just call OnChanged() from any property setter.
    /// 
    /// This is the foundation of MVVM data binding - when properties change,
    /// the UI automatically updates to reflect the new values.
    /// </summary>
    /// <param name="m">Property name (automatically provided by CallerMemberName)</param>
    private void OnChanged([CallerMemberName] string? m = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));
}