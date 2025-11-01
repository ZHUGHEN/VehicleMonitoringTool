using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core;
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace CarTelemetry.Desktop.ViewModels;

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly IObdPoller _poller;
    private readonly IAgentService _agentService;
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _rpm, _speedMph, _coolantC;
    private bool _isConnected = false;
    private bool _isTransmitting = false;
    private DateTime _lastUpdateTime = DateTime.Now;

    public double Rpm { get => _rpm; private set { _rpm = value; OnChanged(); } }
    public double SpeedMph { get => _speedMph; private set { _speedMph = value; OnChanged(); } }
    public double CoolantC { get => _coolantC; private set { _coolantC = value; OnChanged(); } }
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnChanged(); } }
    public bool IsTransmitting { get => _isTransmitting; private set { _isTransmitting = value; OnChanged(); } }
    public DateTime LastUpdateTime { get => _lastUpdateTime; private set { _lastUpdateTime = value; OnChanged(); } }

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

    public MainViewModel(IObdPoller poller, IAgentService agentService)
    {
        _poller = poller;
        _agentService = agentService;
        
        // Subscribe to transmission state changes
        _agentService.TransmissionStateChanged += (s, isTransmitting) =>
        {
            IsTransmitting = isTransmitting;
        };
        
        _ = RunAsync();
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