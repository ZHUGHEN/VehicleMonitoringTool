using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core.Obd;

/// <summary>
/// Development adapter that simulates a rolling drive cycle and representative DTC responses.
/// </summary>
public sealed class MockObdAdapter : IObdAdapter
{
    private readonly Random _r = new();
    private double _currentRpm = 800;
    private double _currentSpeedMph = 0;
    private int _currentGear = 1;
    private bool _accelerating = true;
    private DateTime _lastShiftTime = DateTime.UtcNow;
    private bool _isShifting = false;
    private double _targetRpmAfterShift = 0;
    private double _shiftStartRpm = 0;
    private DateTime _shiftStartTime = DateTime.UtcNow;
    private bool _revMatching = false;
    
    private readonly double[] _gearTopSpeeds = { 0, 30, 60, 90, 120, 150, 180 };
    private readonly double _redlineRpm = 7000;
    private readonly double _idleRpm = 800;
    private readonly double _shiftDurationSeconds = 0.5;
    
    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<string> SendRawAsync(string command, CancellationToken ct)
    {
        return Task.FromResult(command switch
        {
            "03" => "43 01 33 01 30 02 03 03 00",
            
            "07" => "47 04 20 00 00",
            
            "0A" => "4A 04 42 00 00",
            
            "04" => "44",
            
            _ => ""
        });
    }

    public Task<double?> ReadRpmAsync(CancellationToken ct)
    {
        return Task.FromResult<double?>(_currentRpm);
    }

    public Task<double?> ReadSpeedKmhAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastShift = now - _lastShiftTime;
        
        // Simulate a six-speed pull, coast-down, and downshift cycle for dashboard testing.
        if (_isShifting)
        {
            var shiftProgress = (now - _shiftStartTime).TotalSeconds / _shiftDurationSeconds;
            
            if (shiftProgress >= 1.0)
            {
                _currentRpm = _targetRpmAfterShift;
                _isShifting = false;
                _revMatching = false;
            }
            else
            {
                _currentRpm = _shiftStartRpm + (_targetRpmAfterShift - _shiftStartRpm) * shiftProgress;
                
                if (_revMatching && _r.NextDouble() < 0.3)
                {
                    _currentRpm += _r.Next(200, 500);
                }
            }
            
            if (_accelerating)
            {
                _currentSpeedMph += _r.NextDouble() * 0.3;
            }
            else
            {
                _currentSpeedMph -= _r.NextDouble() * 0.8;
            }
        }
        else if (_accelerating)
        {
            var rpmIncrease = _r.Next(40, 120);
            _currentRpm += rpmIncrease;
            
            var rpmProgress = (_currentRpm - _idleRpm) / (_redlineRpm - _idleRpm);
            rpmProgress = Math.Clamp(rpmProgress, 0, 1);
            
            var currentGearTopSpeed = _gearTopSpeeds[_currentGear];
            var previousGearTopSpeed = _currentGear > 1 ? _gearTopSpeeds[_currentGear - 1] : 0;
            
            _currentSpeedMph = previousGearTopSpeed + (currentGearTopSpeed - previousGearTopSpeed) * rpmProgress;
            
            var shouldUpshift = _currentRpm >= _redlineRpm 
                               && _currentGear < 6 
                               && timeSinceLastShift.TotalSeconds > 1;
            
            if (shouldUpshift)
            {
                StartUpshift();
            }
            
            if (_currentGear == 6 && _currentRpm >= _redlineRpm)
            {
                _accelerating = false;
            }
        }
        else
        {
            _currentRpm -= _r.Next(60, 150);
            _currentSpeedMph -= _r.NextDouble() * 2.5;
            
            var shouldDownshift = false;
            if (_currentGear > 1)
            {
                var previousGearTopSpeed = _gearTopSpeeds[_currentGear - 1];
                var currentGearMinSpeed = previousGearTopSpeed * 0.7;
                
                shouldDownshift = (_currentSpeedMph <= currentGearMinSpeed || _currentRpm <= 4000)
                                 && timeSinceLastShift.TotalSeconds > 1 
                                 && !_isShifting;
            }
            
            if (shouldDownshift)
            {
                StartDownshift();
            }
            
            if (_currentSpeedMph <= 2 && _currentRpm <= 1000)
            {
                _accelerating = true;
                _currentGear = 1;
                _currentRpm = _idleRpm;
                _currentSpeedMph = 0;
                _lastShiftTime = now;
            }
        }
        
        _currentRpm = Math.Clamp(_currentRpm, _idleRpm, 8000);
        _currentSpeedMph = Math.Max(_currentSpeedMph, 0);
        
        var speedKmh = _currentSpeedMph * 1.60934;
        
        return Task.FromResult<double?>(speedKmh);
    }
    
    private void StartUpshift()
    {
        _isShifting = true;
        _shiftStartRpm = _currentRpm;
        _shiftStartTime = DateTime.UtcNow;
        _lastShiftTime = DateTime.UtcNow;
        _currentGear++;
        
        var rpmDrop = _r.Next(1500, 2001);
        _targetRpmAfterShift = Math.Max(_shiftStartRpm - rpmDrop, 1500);
        _revMatching = false;
    }
    
    private void StartDownshift()
    {
        _isShifting = true;
        _shiftStartRpm = _currentRpm;
        _shiftStartTime = DateTime.UtcNow;
        _lastShiftTime = DateTime.UtcNow;
        _currentGear--;
        
        var rpmIncrease = _r.Next(1000, 1800);
        _targetRpmAfterShift = Math.Min(_shiftStartRpm + rpmIncrease, 5500);
        _revMatching = true;
    }

    public Task<double?> ReadCoolantCAsync(CancellationToken ct)
        => Task.FromResult<double?>(80 + _r.NextDouble() * 5);
}

