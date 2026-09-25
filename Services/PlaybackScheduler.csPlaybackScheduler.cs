using System;
using System.Timers;

namespace SBtools.Services;

public class PlaybackScheduler : IDisposable
{
    private readonly Timer _timer;
    private readonly Action _onTrigger;

    private TimeSpan _startTime = new TimeSpan(19, 0, 0);
    private TimeSpan _endTime   = new TimeSpan(19, 30, 0);
    private bool _enabled;

    private DateTime _lastFireDate = DateTime.MinValue;

    public bool Enabled { get => _enabled; set => _enabled = value; }
    public TimeSpan StartTime { get => _startTime; set => _startTime = value; }
    public TimeSpan EndTime { get => _endTime; set => _endTime = value; }

    /// <summary>触发时打开的频道（可选，null 表示用默认频道）</summary>
    public CctvChannel? Channel { get; set; }

    public PlaybackScheduler(Action onTrigger)
    {
        _onTrigger = onTrigger;
        _timer = new Timer(10_000) { AutoReset = true };
        _timer.Elapsed += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        if (!_enabled) return;

        var now = DateTime.Now;
        if (_lastFireDate.Date == now.Date) return;

        var t = now.TimeOfDay;
        bool inWindow = _startTime <= _endTime
            ? (t >= _startTime && t <= _endTime)
            : (t >= _startTime || t <= _endTime);

        if (inWindow)
        {
            _lastFireDate = now;
            _onTrigger?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}