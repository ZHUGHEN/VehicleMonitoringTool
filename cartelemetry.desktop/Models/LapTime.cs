// System import for DateTime and TimeSpan types
using System;

namespace CarTelemetry.Desktop.Models;

/// <summary>
/// LapTime represents a single recorded lap in the lap timing system.
/// This is a data model (the "Model" in MVVM) that encapsulates all information about one completed lap.
/// 
/// Key Responsibilities:
/// 1. Storing the essential data for each lap (number, time, status, when it occurred)
/// 2. Providing a clean data structure for lap time collections
/// 3. Supporting best lap identification and highlighting
/// 4. Enabling lap time analysis and session tracking
/// 
/// Usage in the Application:
/// - Created in MainViewModel.LapTimer() when a lap is completed
/// - Stored in MainViewModel.LapTimes ObservableCollection
/// - Displayed in LapTimerView.axaml lap history list
/// - Used for best lap calculations and visual highlighting
/// 
/// Design Pattern: Data Transfer Object (DTO)
/// This is a simple data container with no business logic, used to transfer
/// lap information between the timing system and the UI display.
/// </summary>
public class LapTime
{
    /// <summary>
    /// Sequential lap number within the current timing session.
    /// Starts at 1 for the first lap and increments with each completed lap.
    /// Used for display purposes and lap identification in the UI list.
    /// </summary>
    public int LapNumber { get; set; }
    
    /// <summary>
    /// Duration of this lap from start to finish.
    /// Measured using System.Diagnostics.Stopwatch for high precision timing.
    /// Displayed in the format "MM:SS.FFF" (minutes:seconds.milliseconds) in the UI.
    /// This is the core performance metric for lap timing.
    /// </summary>
    public TimeSpan Time { get; set; }
    
    /// <summary>
    /// Indicates whether this lap is currently the best (fastest) lap in the session.
    /// Only one lap at a time can be marked as the best lap.
    /// When a new best lap is recorded, the previous best lap's IsBest flag is set to false.
    /// Used by the UI to highlight the best lap with special styling (different color, bold text, etc.).
    /// </summary>
    public bool IsBest { get; set; }
    
    /// <summary>
    /// Timestamp when this lap was completed.
    /// Recorded at the moment the lap button is pressed in the UI.
    /// Useful for session analysis, lap time exports, and determining when the lap occurred.
    /// Could be used for future features like lap time averaging over time periods.
    /// </summary>
    public DateTime Timestamp { get; set; }
}