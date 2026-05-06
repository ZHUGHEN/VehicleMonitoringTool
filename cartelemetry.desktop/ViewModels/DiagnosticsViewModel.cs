using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.Configuration;
using CarTelemetry.Desktop.Services;

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// Presents DTC reads, clearing operations, and optional AI-assisted diagnostics.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IDtcService _dtc;
    private readonly IDiagnosticAiService _diagnosticAi;
    private readonly OpenAiConfiguration _openAiConfig;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private string _analysisResult = "";
    [ObservableProperty] private bool _hasAnalysisResult;

    public ObservableCollection<DtcCode> Stored { get; } = new();
    public ObservableCollection<DtcCode> Pending { get; } = new();
    public ObservableCollection<DtcCode> Permanent { get; } = new();

    public DiagnosticsViewModel(
        IDtcService dtc,
        IDiagnosticAiService diagnosticAi,
        OpenAiConfiguration openAiConfig)
    {
        _dtc = dtc;
        _diagnosticAi = diagnosticAi;
        _openAiConfig = openAiConfig;
    }

    [RelayCommand]
    private async Task ReadAllAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Reading DTCs…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await LoadAllListsAsync(cts.Token);
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
            await LoadAllListsAsync(cts.Token);
        }
        catch (TaskCanceledException) { Status = "Canceled"; }
        catch (System.Exception ex)   { Status = "Error: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AnalyzeWithChatGptAsync()
    {
        if (IsBusy) return;
        IsBusy = true; Status = "Asking ChatGPT to analyze DTCs...";
        AnalysisResult = "";
        HasAnalysisResult = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            if (Stored.Count + Pending.Count + Permanent.Count == 0)
            {
                await LoadAllListsAsync(cts.Token);
            }

            var codes = Stored.Concat(Pending).Concat(Permanent).ToArray();
            AnalysisResult = await _diagnosticAi.AnalyzeAsync(_openAiConfig.VehicleModel, codes, cts.Token);
            HasAnalysisResult = true;
            Status = "ChatGPT analysis complete";
        }
        catch (TaskCanceledException) { Status = "Canceled"; }
        catch (System.Exception ex)   { Status = "Error: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadAllListsAsync(CancellationToken ct)
    {
        await LoadListAsync(Stored,    DtcClass.Stored,    ct);
        await LoadListAsync(Pending,   DtcClass.Pending,   ct);
        await LoadListAsync(Permanent, DtcClass.Permanent, ct);
    }

    private async Task LoadListAsync(ObservableCollection<DtcCode> target, DtcClass kind, CancellationToken ct)
    {
        target.Clear();
        var list = await _dtc.ReadAsync(kind, ct);
        foreach (var item in list) target.Add(item);
    }
}

