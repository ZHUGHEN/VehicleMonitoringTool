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
    private readonly CancellationToken _appCt;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Idle";

    public ObservableCollection<DtcCode> Stored { get; } = new();
    public ObservableCollection<DtcCode> Pending { get; } = new();
    public ObservableCollection<DtcCode> Permanent { get; } = new();

    public DiagnosticsViewModel(IObdAdapter obd, IDtcService dtc, CancellationToken appCt)
    {
        _obd = obd;
        _dtc = dtc;
        _appCt = appCt;
    }

    [RelayCommand]
    private async Task ReadAllAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Reading DTCs…";
        try
        {
            await LoadListAsync(Stored,   DtcClass.Stored,   _appCt);
            await LoadListAsync(Pending,  DtcClass.Pending,  _appCt);
            await LoadListAsync(Permanent,DtcClass.Permanent,_appCt);
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
            var ok = await _dtc.ClearAsync(_appCt);
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
