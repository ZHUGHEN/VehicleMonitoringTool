using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core;

namespace CarTelemetry.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IObdPoller _poller;
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _rpm, _speedMph, _coolantC;
    private bool _isConnected = false;
    private DateTime _lastUpdateTime = DateTime.Now;

    public double Rpm { get => _rpm; private set { _rpm = value; OnChanged(); } }
    public double SpeedMph { get => _speedMph; private set { _speedMph = value; OnChanged(); } }
    public double CoolantC { get => _coolantC; private set { _coolantC = value; OnChanged(); } }
    public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnChanged(); } }
    public DateTime LastUpdateTime { get => _lastUpdateTime; private set { _lastUpdateTime = value; OnChanged(); } }

    public MainViewModel(IObdPoller poller)
    {
        _poller = poller;
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
