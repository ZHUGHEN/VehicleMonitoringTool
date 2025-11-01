using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core;
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.Configuration;
using CarTelemetry.Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace CarTelemetry.Desktop.ViewModels;

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly IObdPoller _poller;
    private readonly IAgentService _agentService;
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _rpm, _speedMph, _coolantC;
    private bool _isConnected = false;
    private bool _isTransmitting = false;
    private bool _isShowingSettings = false;
    private DateTime _lastUpdateTime = DateTime.Now;
    private GaugeConfiguration _gaugeConfig = GaugeConfiguration.CreateDefault();

    public double Rpm { get => _rpm; private set { _rpm = value; OnChanged(); } }
    public double SpeedMph { get => _speedMph; private set { _speedMph = value; OnChanged(); } }
    public double CoolantC { get => _coolantC; private set { _coolantC = value; OnChanged(); } }
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnChanged(); } }
    public bool IsTransmitting { get => _isTransmitting; private set { _isTransmitting = value; OnChanged(); } }
    public bool IsShowingSettings { get => _isShowingSettings; private set { _isShowingSettings = value; OnChanged(); } }
    public DateTime LastUpdateTime { get => _lastUpdateTime; private set { _lastUpdateTime = value; OnChanged(); } }
    
    // Gauge Configuration
    public GaugeConfiguration GaugeConfig 
    { 
        get => _gaugeConfig; 
        private set 
        { 
            _gaugeConfig = value; 
            OnChanged(); 
            // Notify that all gauge slot properties have changed
            OnChanged(nameof(GaugeSlot1));
            OnChanged(nameof(GaugeSlot2));
            OnChanged(nameof(GaugeSlot3));
            OnChanged(nameof(GaugeSlot4));
            OnChanged(nameof(GaugeSlot5));
            OnChanged(nameof(GaugeSlot6));
            // Update gauge ViewModels
            UpdateGaugeViewModels();
        } 
    }
    
    // Individual slot properties for binding
    public GaugeType GaugeSlot1 => GaugeConfig.GaugeSlot1;
    public GaugeType GaugeSlot2 => GaugeConfig.GaugeSlot2;
    public GaugeType GaugeSlot3 => GaugeConfig.GaugeSlot3;
    public GaugeType GaugeSlot4 => GaugeConfig.GaugeSlot4;
    public GaugeType GaugeSlot5 => GaugeConfig.GaugeSlot5;
    public GaugeType GaugeSlot6 => GaugeConfig.GaugeSlot6;
    
    // Gauge ViewModels for dynamic display
    public ObservableCollection<GaugeViewModel> Gauges { get; } = new();

    private void UpdateGaugeViewModels()
    {
        Gauges.Clear();
        
        // Create 6 gauge ViewModels based on configuration
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

    public MainViewModel(IObdPoller poller, IAgentService agentService)
    {
        _poller = poller;
        _agentService = agentService;
        
        // Subscribe to transmission state changes
        _agentService.TransmissionStateChanged += (s, isTransmitting) =>
        {
            IsTransmitting = isTransmitting;
        };
        
        // Initialize gauges with default configuration
        UpdateGaugeViewModels();
        
        // Load gauge configuration
        _ = Task.Run(LoadGaugeConfigurationAsync);
        
        _ = RunAsync();
    }

    private async Task LoadGaugeConfigurationAsync()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarTelemetry");
            var filePath = Path.Combine(appDataPath, "gauge-config.json");
            
            if (!File.Exists(filePath))
            {
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
            await foreach (var t in _poller.StreamAsync(cts.Token))
            {
                Rpm = t.Rpm ?? 0;
                SpeedMph = (t.SpeedKmh ?? 0) * 0.621371;
                CoolantC = t.CoolantC ?? 0;
                LastUpdateTime = DateTime.Now;
                IsConnected = true;
                
                // Update all gauge ViewModels with new telemetry data
                foreach (var gauge in Gauges)
                {
                    gauge.UpdateValue(Rpm, SpeedMph, CoolantC, 
                        engineLoad: 45, // Mock data for now
                        fuelPressure: 35, // Mock data for now  
                        fuelTrim: 5); // Mock data for now
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