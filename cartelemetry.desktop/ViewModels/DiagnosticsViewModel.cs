using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CarTelemetry.Core.Obd;

namespace CarTelemetry.Desktop.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IObdAdapter _obd;
    private readonly IDtcService _dtc;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Idle";

    public ObservableCollection<DtcCode> Stored { get; } = new();
    public ObservableCollection<DtcCode> Pending { get; } = new();
    public ObservableCollection<DtcCode> Permanent { get; } = new();

    public DiagnosticsViewModel(IObdAdapter obd, IDtcService dtc)
    {
        _obd = obd;
        _dtc = dtc;
    }

    [RelayCommand]
    private async Task ReadAllAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Reading DTCs…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await LoadListAsync(Stored,   DtcClass.Stored,   cts.Token);
            await LoadListAsync(Pending,  DtcClass.Pending,  cts.Token);
            await LoadListAsync(Permanent,DtcClass.Permanent,cts.Token);
            Status = "Read complete";
        }
        catch (TaskCanceledException) { Status = "Canceled"; }
        catch (System.Exception ex)   { Status = "Error: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Clearing DTCs (Mode 04)…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ok = await _dtc.ClearAsync(cts.Token);
            Status = ok ? "Clear request sent." : "Clear request failed.";
            // After clearing, refresh lists (they may come back empty until faults reoccur)
            await ReadAllAsync();
        }
        catch (TaskCanceledException) { Status = "Canceled"; }
        catch (System.Exception ex)   { Status = "Error: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadListAsync(ObservableCollection<DtcCode> target, DtcClass kind, CancellationToken ct)
    {
        target.Clear();
        var list = await _dtc.ReadAsync(kind, ct);
        foreach (var item in list) target.Add(item);
    }
}
