using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Models;
using CarTelemetry.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTelemetry.Desktop.ViewModels;

public sealed record ServiceTypeOption(string Name);

public sealed record ServiceMonthOption(int Number, string Name);

/// <summary>
/// Manages touch-screen service record entry and persisted maintenance history.
/// </summary>
public partial class ServiceRecordsViewModel : ObservableObject
{
    private readonly IServiceRecordStore _store;

    [ObservableProperty] private ServiceTypeOption _selectedServiceType;
    [ObservableProperty] private ServiceMonthOption _selectedMonth;
    [ObservableProperty] private int _selectedYear;
    [ObservableProperty] private string _mileageText = "";
    [ObservableProperty] private bool _isEntryOpen;
    [ObservableProperty] private bool _isMileageKeypadOpen;
    [ObservableProperty] private bool _isConfirmingEntry;
    [ObservableProperty] private bool _isEditingEntry;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private string _status = "";

    private ServiceRecord? _pendingRecord;
    private ServiceRecord? _editingRecord;
    private ServiceRecord? _deleteRecord;

    public ObservableCollection<ServiceRecord> Records { get; } = new();

    public ObservableCollection<ServiceTypeOption> ServiceTypes { get; } = new()
    {
        new ServiceTypeOption("Oil Change"),
        new ServiceTypeOption("Transmission Fluid Change"),
        new ServiceTypeOption("Spark Plug Replacement")
    };

    public ObservableCollection<ServiceMonthOption> Months { get; } = new()
    {
        new ServiceMonthOption(1, "January"),
        new ServiceMonthOption(2, "February"),
        new ServiceMonthOption(3, "March"),
        new ServiceMonthOption(4, "April"),
        new ServiceMonthOption(5, "May"),
        new ServiceMonthOption(6, "June"),
        new ServiceMonthOption(7, "July"),
        new ServiceMonthOption(8, "August"),
        new ServiceMonthOption(9, "September"),
        new ServiceMonthOption(10, "October"),
        new ServiceMonthOption(11, "November"),
        new ServiceMonthOption(12, "December")
    };

    public ObservableCollection<int> Years { get; }

    public bool HasRecords => Records.Count > 0;

    public bool CanReviewEntry => MileageText.Length is >= 4 and <= 6;

    public string MileageDisplayText => string.IsNullOrWhiteSpace(MileageText)
        ? "Tap to enter mileage"
        : MileageText;

    public string KeypadMileageDisplay => string.IsNullOrWhiteSpace(MileageText)
        ? "----"
        : MileageText;

    public string PendingSummary => _pendingRecord == null
        ? ""
        : $"{_pendingRecord.Type} at {_pendingRecord.DisplayMileage} on {_pendingRecord.DisplayDate}";

    public string EntryTitle => IsEditingEntry
        ? "Edit Maintenance Record"
        : "New Maintenance Record";

    public string ReviewButtonText => IsEditingEntry
        ? "Review Changes"
        : "Review";

    public string DeleteSummary => _deleteRecord == null
        ? ""
        : $"{_deleteRecord.Type} at {_deleteRecord.DisplayMileage} on {_deleteRecord.DisplayDate}";

    public ServiceRecordsViewModel(IServiceRecordStore store)
    {
        _store = store;

        var now = DateTime.Now;
        _selectedServiceType = ServiceTypes[0];
        _selectedMonth = Months.First(month => month.Number == now.Month);
        _selectedYear = now.Year;

        Years = new ObservableCollection<int>(
            Enumerable.Range(now.Year - 30, 36).Reverse());

        Records.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecords));

        _ = Task.Run(LoadAsync);
    }

    partial void OnMileageTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanReviewEntry));
        OnPropertyChanged(nameof(MileageDisplayText));
        OnPropertyChanged(nameof(KeypadMileageDisplay));
    }

    [RelayCommand]
    private void OpenEntry()
    {
        ResetEntry();
        IsEntryOpen = true;
        Status = "";
    }

    [RelayCommand]
    private void CancelEntry()
    {
        IsEntryOpen = false;
        IsMileageKeypadOpen = false;
        IsConfirmingEntry = false;
        IsEditingEntry = false;
        _editingRecord = null;
        _pendingRecord = null;
    }

    [RelayCommand]
    private void EditRecord(ServiceRecord record)
    {
        _editingRecord = record;
        IsEditingEntry = true;
        SelectedServiceType = ServiceTypes.FirstOrDefault(type => type.Name == record.Type) ?? ServiceTypes[0];
        SelectedMonth = Months.FirstOrDefault(month => month.Number == record.Month) ?? Months[0];
        SelectedYear = record.Year;
        MileageText = record.Mileage.ToString();
        IsConfirmingEntry = false;
        IsMileageKeypadOpen = false;
        IsEntryOpen = true;
        Status = "";
    }

    [RelayCommand]
    private void DeleteRecord(ServiceRecord record)
    {
        _deleteRecord = record;
        OnPropertyChanged(nameof(DeleteSummary));
        IsConfirmingDelete = true;
    }

    [RelayCommand]
    private void OpenMileageKeypad()
    {
        IsMileageKeypadOpen = true;
    }

    [RelayCommand]
    private void CloseMileageKeypad()
    {
        IsMileageKeypadOpen = false;
    }

    [RelayCommand]
    private void AddMileageDigit(string digit)
    {
        if (MileageText.Length >= 6 || digit.Length != 1 || !char.IsDigit(digit[0]))
        {
            return;
        }

        MileageText += digit;
    }

    [RelayCommand]
    private void BackspaceMileage()
    {
        if (MileageText.Length > 0)
        {
            MileageText = MileageText[..^1];
        }
    }

    [RelayCommand]
    private void ClearMileage()
    {
        MileageText = "";
    }

    [RelayCommand]
    private void ReviewEntry()
    {
        if (!CanReviewEntry || !int.TryParse(MileageText, out var mileage))
        {
            Status = "Mileage must be 4 to 6 digits.";
            return;
        }

        _pendingRecord = new ServiceRecord
        {
            Type = SelectedServiceType.Name,
            Mileage = mileage,
            Month = SelectedMonth.Number,
            Year = SelectedYear,
            CreatedAt = DateTime.Now
        };

        OnPropertyChanged(nameof(PendingSummary));
        IsConfirmingEntry = true;
        IsMileageKeypadOpen = false;
    }

    [RelayCommand]
    private void CancelConfirmation()
    {
        IsConfirmingEntry = false;
        _pendingRecord = null;
        OnPropertyChanged(nameof(PendingSummary));
    }

    [RelayCommand]
    private async Task ConfirmEntryAsync()
    {
        if (_pendingRecord == null)
        {
            return;
        }

        if (_editingRecord != null)
        {
            var existingIndex = Records.IndexOf(_editingRecord);

            if (existingIndex >= 0)
            {
                Records[existingIndex] = _pendingRecord;
            }
        }
        else
        {
            Records.Add(_pendingRecord);
        }

        SortRecords();
        await _store.SaveAsync(Records.ToArray());

        Status = IsEditingEntry
            ? "Service record updated."
            : "Service record saved.";
        IsEntryOpen = false;
        IsConfirmingEntry = false;
        IsEditingEntry = false;
        _editingRecord = null;
        _pendingRecord = null;
        OnPropertyChanged(nameof(PendingSummary));
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmingDelete = false;
        _deleteRecord = null;
        OnPropertyChanged(nameof(DeleteSummary));
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_deleteRecord == null)
        {
            return;
        }

        Records.Remove(_deleteRecord);
        await _store.SaveAsync(Records.ToArray());

        Status = "Service record deleted.";
        IsConfirmingDelete = false;
        _deleteRecord = null;
        OnPropertyChanged(nameof(DeleteSummary));
    }

    private async Task LoadAsync()
    {
        var records = await _store.LoadAsync();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Records.Clear();

            foreach (var record in records)
            {
                Records.Add(record);
            }
        });
    }

    private void SortRecords()
    {
        var sorted = Records
            .OrderBy(record => record.Year)
            .ThenBy(record => record.Month)
            .ThenBy(record => record.Mileage)
            .ThenBy(record => record.CreatedAt)
            .ToArray();

        Records.Clear();

        foreach (var record in sorted)
        {
            Records.Add(record);
        }
    }

    private void ResetEntry()
    {
        var now = DateTime.Now;
        SelectedServiceType = ServiceTypes[0];
        SelectedMonth = Months.First(month => month.Number == now.Month);
        SelectedYear = now.Year;
        MileageText = "";
        IsConfirmingEntry = false;
        IsMileageKeypadOpen = false;
        IsEditingEntry = false;
        _editingRecord = null;
        _pendingRecord = null;
        OnPropertyChanged(nameof(EntryTitle));
        OnPropertyChanged(nameof(ReviewButtonText));
        OnPropertyChanged(nameof(PendingSummary));
    }

    partial void OnIsEditingEntryChanged(bool value)
    {
        OnPropertyChanged(nameof(EntryTitle));
        OnPropertyChanged(nameof(ReviewButtonText));
    }
}
