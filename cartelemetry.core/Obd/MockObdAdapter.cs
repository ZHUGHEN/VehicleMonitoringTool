using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core.Obd;

public sealed class MockObdAdapter : IObdAdapter
{
    private readonly Random _r = new();
    private double _currentRpm = 800; // Start at idle RPM
    private double _currentSpeedMph = 0; // Track speed independently
    private int _currentGear = 1; // Start in 1st gear
    private bool _accelerating = true;
    private DateTime _lastShiftTime = DateTime.UtcNow;
    private bool _isShifting = false;
    private double _targetRpmAfterShift = 0;
    private double _shiftStartRpm = 0;
    private DateTime _shiftStartTime = DateTime.UtcNow;
    private bool _revMatching = false;
    
    // Gear ratios and top speeds
    private readonly double[] _gearTopSpeeds = { 0, 30, 60, 90, 120, 150, 180 }; // Index 0 unused, gears 1-6
    private readonly double _redlineRpm = 7000;
    private readonly double _idleRpm = 800;
    private readonly double _shiftDurationSeconds = 0.5;
    
    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Mock DTC Code Implementation
    public Task<string> SendRawAsync(string command, CancellationToken ct)
{
    return Task.FromResult(command switch
    {
        "03" => "43 01 33 00 00 00", // Example: P0133
        "07" => "47 00 00 00 00 00",
        "0A" => "4A 00 00 00 00 00",
        "04" => "OK",
        _    => ""
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
        
        // Handle shifting in progress
        if (_isShifting)
        {
            var shiftProgress = (now - _shiftStartTime).TotalSeconds / _shiftDurationSeconds;
            
            if (shiftProgress >= 1.0)
            {
                // Shift complete
                _currentRpm = _targetRpmAfterShift;
                _isShifting = false;
                _revMatching = false;
            }
            else
            {
                // Interpolate RPM during shift
                _currentRpm = _shiftStartRpm + (_targetRpmAfterShift - _shiftStartRpm) * shiftProgress;
                
                // Add rev-matching blips during downshifts
                if (_revMatching && _r.NextDouble() < 0.3) // 30% chance of blip per reading
                {
                    _currentRpm += _r.Next(200, 500); // Throttle blip
                }
            }
            
            // During gear change, speed continues due to momentum (minimal change)
            if (_accelerating)
            {
                _currentSpeedMph += _r.NextDouble() * 0.3; // Very slight increase during shift
            }
            else
            {
                _currentSpeedMph -= _r.NextDouble() * 0.8; // Slight deceleration during shift
            }
        }
        else if (_accelerating)
        {
            // Normal acceleration - RPM and speed increase together in sync
            var rpmIncrease = _r.Next(40, 120);
            _currentRpm += rpmIncrease;
            
            // Calculate speed based on RPM position within current gear
            // At redline (7000 RPM), we should be at the gear's top speed
            var rpmProgress = (_currentRpm - _idleRpm) / (_redlineRpm - _idleRpm); // 0.0 to 1.0
            rpmProgress = Math.Clamp(rpmProgress, 0, 1);
            
            var currentGearTopSpeed = _gearTopSpeeds[_currentGear];
            var previousGearTopSpeed = _currentGear > 1 ? _gearTopSpeeds[_currentGear - 1] : 0;
            
            // Speed should range from previous gear's top speed to current gear's top speed
            _currentSpeedMph = previousGearTopSpeed + (currentGearTopSpeed - previousGearTopSpeed) * rpmProgress;
            
            // Check for upshift when we reach redline (which means we're at gear's top speed)
            var shouldUpshift = _currentRpm >= _redlineRpm 
                               && _currentGear < 6 
                               && timeSinceLastShift.TotalSeconds > 1;
            
            if (shouldUpshift)
            {
                StartUpshift();
            }
            
            // Start decelerating if we're in 6th gear and at redline
            if (_currentGear == 6 && _currentRpm >= _redlineRpm)
            {
                _accelerating = false;
            }
        }
        else
        {
            // Decelerate both RPM and speed
            _currentRpm -= _r.Next(60, 150);
            _currentSpeedMph -= _r.NextDouble() * 2.5; // Speed decreases
            
            // Check for downshift when speed drops below optimal range for current gear
            var shouldDownshift = false;
            if (_currentGear > 1)
            {
                var previousGearTopSpeed = _gearTopSpeeds[_currentGear - 1];
                var currentGearMinSpeed = previousGearTopSpeed * 0.7; // 70% of previous gear's top speed
                
                shouldDownshift = (_currentSpeedMph <= currentGearMinSpeed || _currentRpm <= 4000) // Motorsports downshift at 4000 RPM
                                 && timeSinceLastShift.TotalSeconds > 1 
                                 && !_isShifting;
            }
            
            if (shouldDownshift)
            {
                StartDownshift();
            }
            
            // Check if we've come to a complete stop
            if (_currentSpeedMph <= 2 && _currentRpm <= 1000) // Nearly stopped
            {
                // Reset to start new cycle
                _accelerating = true;
                _currentGear = 1;
                _currentRpm = _idleRpm;
                _currentSpeedMph = 0;
                _lastShiftTime = now;
            }
        }
        
        // Keep RPM and speed in realistic bounds
        _currentRpm = Math.Clamp(_currentRpm, _idleRpm, 8000);
        _currentSpeedMph = Math.Max(_currentSpeedMph, 0); // Don't go negative
        
        // Convert MPH to KMH
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
        
        // Calculate target RPM after upshift (1500-2000 RPM drop)
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
        
        // Calculate target RPM after downshift (higher RPM due to lower gear)
        var rpmIncrease = _r.Next(1000, 1800);
        _targetRpmAfterShift = Math.Min(_shiftStartRpm + rpmIncrease, 5500);
        _revMatching = true; // Enable rev-matching blips
    }

    public Task<double?> ReadCoolantCAsync(CancellationToken ct)
        => Task.FromResult<double?>(80 + _r.NextDouble() * 5);
}
