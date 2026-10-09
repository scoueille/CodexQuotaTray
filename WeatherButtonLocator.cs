using System.Diagnostics;
using System.Windows.Automation;

namespace CodexQuotaTray;

/// <summary>Queries Explorer accessibility on a dedicated MTA thread without owning any windows.</summary>
internal sealed class WeatherButtonLocator : IDisposable
{
    private readonly object _gate = new();
    private readonly AutoResetEvent _request = new(false);
    private readonly Thread _worker;
    private nint _taskbar;
    private int _left;
    private long _lastRequest;
    private bool _disposed;

    /// <summary>Starts one background worker; a stalled accessibility call cannot block the UI or spawn more workers.</summary>
    public WeatherButtonLocator()
    {
        _worker = new Thread(Run) { IsBackground = true, Name = "Taskbar weather accessibility" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    /// <summary>Returns cached coordinates immediately and queues at most one refresh every five seconds.</summary>
    public bool TryGetLeft(nint taskbar, out int left)
    {
        lock (_gate)
        {
            var changed = _taskbar != taskbar;
            if (changed)
            {
                _taskbar = taskbar;
                _left = 0;
            }
            if (!_disposed && (changed || Stopwatch.GetElapsedTime(_lastRequest).TotalSeconds >= 5))
            {
                _lastRequest = Stopwatch.GetTimestamp();
                _request.Set();
            }
            left = _left;
            return left > 0;
        }
    }

    /// <summary>Keeps all AutomationElement instances on this thread and publishes only numeric coordinates.</summary>
    private void Run()
    {
        nint cachedTaskbar = nint.Zero;
        AutomationElement? button = null;
        try
        {
            while (true)
            {
                _request.WaitOne();
                nint taskbar;
                lock (_gate)
                {
                    if (_disposed) return;
                    taskbar = _taskbar;
                }
                if (cachedTaskbar != taskbar)
                {
                    cachedTaskbar = taskbar;
                    button = null;
                }
                var started = Stopwatch.GetTimestamp();
                try
                {
                    button ??= AutomationElement.FromHandle(taskbar).FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "WidgetsButton"));
                    if (button is not null && !button.Current.IsOffscreen)
                    {
                        var bounds = button.Current.BoundingRectangle;
                        if (!bounds.IsEmpty && bounds.Width > 0)
                            lock (_gate)
                                if (!_disposed && _taskbar == taskbar) _left = (int)Math.Round(bounds.Left);
                    }
                }
                catch (Exception error)
                {
                    button = null;
                    TaskbarWidgetController.LogWidgetState($"weather detection unavailable: {error.GetType().Name}");
                }
                var elapsed = Stopwatch.GetElapsedTime(started);
                if (elapsed.TotalSeconds >= 2)
                    TaskbarWidgetController.LogWidgetState($"weather detection slow: {elapsed.TotalMilliseconds:F0} ms");
            }
        }
        finally { _request.Dispose(); }
    }

    /// <summary>Signals shutdown without waiting for a possibly stalled Explorer accessibility call.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _request.Set();
        }
    }
}
