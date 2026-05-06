using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CarTelemetry.Core;
using CarTelemetry.Core.Obd;

using CarTelemetry.Desktop.Configuration;
using CarTelemetry.Desktop.Models;
using CarTelemetry.Desktop.Services;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// Coordinates live telemetry, gauge configuration, transmission state, and lap timing for the main dashboard.
/// </summary>
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly IObdPoller _poller;
    private readonly IAgentService _agentService;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    
    private double _rpm;
    private double _speedMph;
    private double _coolantC;
    
    private bool _isConnected = false;
    private bool _isTransmitting = false;
    private bool _isShowingSettings = false;
    private bool _isShowingScreensaver = false;
    
    private DateTime _lastUpdateTime = DateTime.Now;
    private DateTime _currentTime = DateTime.Now;
    
    private GaugeConfiguration _gaugeConfig = GaugeConfiguration.CreateDefault();

    private Stopwatch _lapTimer = new();
    private readonly DispatcherTimer _lapDisplayTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };
    private TimeSpan _currentLapTime;
    private TimeSpan? _bestLapTime;
    private TimeSpan? _lastLapTime;
    private int _lapCount = 1;

    
    public double Rpm { get => _rpm; private set { _rpm = value; OnChanged(); } }
    
    public double SpeedMph { get => _speedMph; private set { _speedMph = value; OnChanged(); } }
    
    public double CoolantC { get => _coolantC; private set { _coolantC = value; OnChanged(); } }
    
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnChanged(); } }
    
    public bool IsTransmitting { get => _isTransmitting; private set { _isTransmitting = value; OnChanged(); } }
    
    public bool IsShowingSettings { get => _isShowingSettings; private set { _isShowingSettings = value; OnChanged(); } }
    
    public bool IsShowingScreensaver { get => _isShowingScreensaver; private set { _isShowingScreensaver = value; OnChanged(); } }
    
    public DateTime LastUpdateTime { get => _lastUpdateTime; private set { _lastUpdateTime = value; OnChanged(); } }
    
    public DateTime CurrentTime { get => _currentTime; private set { _currentTime = value; OnChanged(); } }
    
    public TimeSpan CurrentLapTime { get => _currentLapTime; private set { _currentLapTime = value; OnChanged(); } }
    
    public TimeSpan? BestLapTime { get => _bestLapTime; private set { _bestLapTime = value; OnChanged(); } }
    
    public TimeSpan? LastLapTime { get => _lastLapTime; private set { _lastLapTime = value; OnChanged(); } }
    
    public int LapCount { get => _lapCount; private set { _lapCount = value; OnChanged(); } }
    
    public ObservableCollection<LapTime> LapTimes { get; } = new();
    
    public GaugeConfiguration GaugeConfig 
    { 
        get => _gaugeConfig; 
        private set 
        { 
            _gaugeConfig = value; 
            OnChanged();
            
            OnChanged(nameof(GaugeSlot1));
            OnChanged(nameof(GaugeSlot2));
            OnChanged(nameof(GaugeSlot3));
            OnChanged(nameof(GaugeSlot4));
            OnChanged(nameof(GaugeSlot5));
            OnChanged(nameof(GaugeSlot6));
            
            UpdateGaugeViewModels();
        } 
    }
    
    public GaugeType GaugeSlot1 => GaugeConfig.GaugeSlot1;
    public GaugeType GaugeSlot2 => GaugeConfig.GaugeSlot2;
    public GaugeType GaugeSlot3 => GaugeConfig.GaugeSlot3;
    public GaugeType GaugeSlot4 => GaugeConfig.GaugeSlot4;
    public GaugeType GaugeSlot5 => GaugeConfig.GaugeSlot5;
    public GaugeType GaugeSlot6 => GaugeConfig.GaugeSlot6;
    
    public ObservableCollection<GaugeViewModel> Gauges { get; } = new();

    private void UpdateGaugeViewModels()
    {
        // Rebuild instead of mutating existing gauges so slot order always matches the saved configuration.
        Gauges.Clear();
        
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot1));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot2));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot3));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot4));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot5));
        Gauges.Add(new GaugeViewModel(GaugeConfig.GaugeSlot6));
    }

    
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

    [RelayCommand]
    public void OpenSettings()
    {
        IsShowingSettings = true;
    }

    [RelayCommand]
    public void GoBack()
    {
        IsShowingSettings = false;
    }

    [RelayCommand]
    public void OpenScreensaver()
    {
        IsShowingScreensaver = true;
    }

    [RelayCommand]
    public void ExitScreensaver()
    {
        IsShowingScreensaver = false;
    }

    
    [RelayCommand]
    public void StartTimer()
    {
        _lapTimer.Restart();
        CurrentLapTime = TimeSpan.Zero;
        _lapDisplayTimer.Start();
    }

    [RelayCommand]
    public void LapTimer()
    {
        if (_lapTimer.IsRunning)
        {
            var lapTime = _lapTimer.Elapsed;
            
            var lap = new LapTime
            {
                LapNumber = LapCount,
                Time = lapTime,
                Timestamp = DateTime.Now
            };
            
            if (!BestLapTime.HasValue || lapTime < BestLapTime.Value)
            {
                // Only one lap should be marked as best so the history list has a single highlight.
                foreach (var existingLap in LapTimes)
                    existingLap.IsBest = false;
                
                lap.IsBest = true;
                BestLapTime = lapTime;
            }
            
            LastLapTime = lapTime;
            LapTimes.Add(lap);
            LapCount++;
            
            _lapTimer.Restart();
            CurrentLapTime = TimeSpan.Zero;
            _lapDisplayTimer.Start();
        }
    }

    [RelayCommand]
    public void StopTimer()
    {
        _lapTimer.Stop();
        _lapDisplayTimer.Stop();
        CurrentLapTime = _lapTimer.Elapsed;
    }

    [RelayCommand]
    public void ResetTimer()
    {
        _lapTimer.Reset();
        _lapDisplayTimer.Stop();
        CurrentLapTime = TimeSpan.Zero;
        BestLapTime = null;
        LastLapTime = null;
        LapCount = 1;
        LapTimes.Clear();
    }

    public MainViewModel(IObdPoller poller, IAgentService agentService)
    {
        _poller = poller;
        _agentService = agentService;
        
        _agentService.TransmissionStateChanged += (s, isTransmitting) =>
        {
            IsTransmitting = isTransmitting;
        };
        
        UpdateGaugeViewModels();

        // Refresh lap display independently from telemetry so milliseconds move smoothly.
        _lapDisplayTimer.Tick += (_, _) =>
        {
            if (_lapTimer.IsRunning)
            {
                CurrentLapTime = _lapTimer.Elapsed;
            }
        };
        
        // Configuration load and telemetry streaming are independent startup tasks.
        _ = Task.Run(LoadGaugeConfigurationAsync);
        
        _ = RunAsync();
    }

    /// <summary>
    /// Loads the persisted gauge layout, falling back to the default dashboard when unavailable.
    /// </summary>
    private async Task LoadGaugeConfigurationAsync()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarTelemetry");
            var filePath = Path.Combine(appDataPath, "gauge-config.json");
            
            if (!File.Exists(filePath))
            {
                // First launch or deleted settings file: keep a complete default dashboard.
                GaugeConfig = GaugeConfiguration.CreateDefault();
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<GaugeConfiguration>(json);
            
            if (config != null)
            {
                GaugeConfig = config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load gauge configuration in MainViewModel: {ex.Message}");
            GaugeConfig = GaugeConfiguration.CreateDefault();
        }
    }

    public async Task RefreshGaugeConfigurationAsync()
    {
        await LoadGaugeConfigurationAsync();
    }

    private async Task RunAsync()
    {
        using var cts = new CancellationTokenSource();
        
        try
        {
            IsConnected = true;
            
            // The UI consumes the latest sample only; historical telemetry is handled by relay/web clients.
            await foreach (var t in _poller.StreamAsync(cts.Token))
            {
                Rpm = t.Rpm ?? 0;
                SpeedMph = (t.SpeedKmh ?? 0) * 0.621371;
                CoolantC = t.CoolantC ?? 0;
                LastUpdateTime = DateTime.Now;
                CurrentTime = DateTime.Now;
                IsConnected = true;
                
                foreach (var gauge in Gauges)
                {
                    // Engine load, fuel pressure, and trim are placeholders until those PIDs are wired in.
                    gauge.UpdateValue(
                        Rpm, SpeedMph, CoolantC,
                        engineLoad: 45,
                        fuelPressure: 35,
                        fuelTrim: 5);
                }
            }
        }
        catch (Exception)
        {
            IsConnected = false;
        }
    }

    private void OnChanged([CallerMemberName] string? m = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));
}

