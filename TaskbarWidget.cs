using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace CodexQuotaTray;

/// <summary>Keeps the quota widget aligned with the Windows taskbar.</summary>
internal sealed class TaskbarWidgetController : IDisposable
{
    private readonly TaskbarWidgetForm _window = new();
    private readonly EmbeddedTaskbarWidget _embeddedWindow = new();
    private readonly Control _dispatchTarget;
    private readonly TaskbarWidgetNative.WinEventProc _shellEventCallback;
    private nint _foregroundHook;
    private nint _windowHideHook;
    private nint _menuEndHook;
    private bool _reattachQueued;
    private bool _visible;
    private bool _disposed;
    private bool _attached;
    private bool _raiseFailed;
    private nint _embeddedTaskbar;
    private DateTimeOffset _embeddedRetryAfter;
    private string? _attachedMode;

    /// <summary>Creates the widget controller with the user's saved visibility preference.</summary>
    public TaskbarWidgetController(bool visible, Control dispatchTarget)
    {
        _visible = visible;
        _dispatchTarget = dispatchTarget;
        _shellEventCallback = OnShellUiChanged;
        _foregroundHook = TaskbarWidgetNative.WatchForegroundChanges(_shellEventCallback);
        _windowHideHook = TaskbarWidgetNative.WatchWindowHides(_shellEventCallback);
        _menuEndHook = TaskbarWidgetNative.WatchMenuEnds(_shellEventCallback);
    }

    /// <summary>Indicates whether the user has chosen to show the taskbar widget.</summary>
    public bool IsVisible => _visible;

    /// <summary>Updates the quota values shown in the widget.</summary>
    public void SetQuota(Quota quota, Color dailyPaceColor)
    {
        _window.SetQuota(quota, dailyPaceColor);
        if (_visible) EnsureAttached();
    }

    /// <summary>Clears the widget values when Codex cannot provide a quota reading.</summary>
    public void SetUnavailable()
    {
        _window.SetUnavailable();
        if (_visible) EnsureAttached();
    }

    /// <summary>Shows or hides the widget and returns whether it is now visible by preference.</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (!_visible)
        {
            HideWindow();
            return;
        }

        EnsureAttached();
    }

    /// <summary>Follows taskbar or display changes and restores the widget after shell UI changes.</summary>
    public bool EnsureAttached()
    {
        if (_disposed || !_visible) return false;
        try
        {
            var taskbar = TaskbarWidgetNative.FindPrimaryTaskbar();
            if (taskbar == nint.Zero || !TaskbarWidgetNative.TryGetTaskbarGeometry(taskbar, out var geometry))
            {
                HideWindow("taskbar geometry unavailable");
                return false;
            }

            var dpi = TaskbarWidgetNative.GetDpiForWindow(taskbar);
            if (dpi == 0) dpi = 96;
            var scale = dpi / 96f;
            var width = (int)MathF.Round(TaskbarWidgetForm.LogicalWidth * scale);
            var height = (int)MathF.Round(TaskbarWidgetForm.LogicalHeight * scale);
            var rightBoundary = geometry.TrayLeft;
            if (TaskbarWidgetNative.TryGetWidgetsButtonLeft(taskbar, out var widgetsLeft) &&
                widgetsLeft > geometry.Taskbar.Left && widgetsLeft < rightBoundary)
                rightBoundary = widgetsLeft;
            var x = rightBoundary - width - (int)MathF.Round(4 * scale);
            var taskbarHeight = geometry.Taskbar.Bottom - geometry.Taskbar.Top;
            if (x < geometry.Taskbar.Left || taskbarHeight < height ||
                geometry.Taskbar.Right - geometry.Taskbar.Left < taskbarHeight)
            {
                HideWindow("widget cannot fit in taskbar geometry");
                return false;
            }

            var y = geometry.Taskbar.Top + Math.Max(0, (taskbarHeight - height) / 2);
            if (_embeddedTaskbar != taskbar)
            {
                _embeddedTaskbar = taskbar;
                _embeddedRetryAfter = DateTimeOffset.MinValue;
            }

            // A layered child follows the taskbar's own visibility and stacking order.
            // Keep the independent window as a fallback for shells that reject parenting.
            if (DateTimeOffset.UtcNow >= _embeddedRetryAfter)
            {
                try
                {
                    var previousContext = TaskbarWidgetNative.SetThreadDpiAwarenessContext(
                        TaskbarWidgetNative.GetWindowDpiAwarenessContext(taskbar));
                    try
                    {
                        using var bitmap = _window.CreateLayeredBitmap(dpi, width, height);
                        if (_embeddedWindow.TryShow(taskbar, geometry.Taskbar, x, y, width, height,
                                bitmap, out var embeddedError))
                        {
                            if (_window.IsHandleCreated && TaskbarWidgetNative.IsWindow(_window.Handle))
                                TaskbarWidgetNative.ShowWindow(_window.Handle, TaskbarWidgetNative.SwHide);
                            if (_attachedMode != "embedded") LogWidgetState("attached embedded");
                            _attachedMode = "embedded";
                            _attached = true;
                            return true;
                        }
                        LogWidgetState($"embedded unavailable: {embeddedError}");
                    }
                    finally
                    {
                        if (previousContext != nint.Zero)
                            TaskbarWidgetNative.SetThreadDpiAwarenessContext(previousContext);
                    }
                }
                catch (Exception error)
                {
                    LogWidgetError(error);
                }

                _embeddedRetryAfter = DateTimeOffset.UtcNow.AddSeconds(30);
            }

            _embeddedWindow.Hide();
            if (!_window.IsHandleCreated)
                CreateHandleForTaskbar(taskbar);
            else if (!TaskbarWidgetNative.IsWindow(_window.Handle))
                RecreateHandleForTaskbar(taskbar);

            var widgetHandle = _window.Handle;
            if (widgetHandle == nint.Zero)
                return false;

            if (!TaskbarWidgetNative.SetWindowPos(widgetHandle, TaskbarWidgetNative.HwndTopmost, x, y, width, height,
                    TaskbarWidgetNative.SwpNoActivate | TaskbarWidgetNative.SwpShowWindow))
            {
                HideWindow($"show failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // Explorer also owns topmost taskbar surfaces. Raise our already-topmost window
            // within that group after Shell flyouts or taskbar clicks change their order.
            var raised = TaskbarWidgetNative.SetWindowPos(widgetHandle, TaskbarWidgetNative.HwndTop, 0, 0, 0, 0,
                TaskbarWidgetNative.SwpNoActivate | TaskbarWidgetNative.SwpNoMove | TaskbarWidgetNative.SwpNoSize);
            if (!raised && !_raiseFailed)
                LogWidgetState($"raise failed: {Marshal.GetLastWin32Error()}");
            _raiseFailed = !raised;
            _window.RenderTaskbarSurface(dpi);
            if (_attachedMode != "overlay") LogWidgetState("attached overlay");
            _attachedMode = "overlay";
            _attached = true;
            return true;
        }
        catch (Exception error)
        {
            LogWidgetError(error);
            HideWindow("attachment error");
            return false;
        }
    }

    /// <summary>Hides both widget display modes when placement is unavailable or the user disables it.</summary>
    private void HideWindow(string? reason = null)
    {
        if (_attached && reason is not null) LogWidgetState(reason);
        _attached = false;
        _attachedMode = null;
        _embeddedWindow.Hide();
        if (_window.IsHandleCreated && TaskbarWidgetNative.IsWindow(_window.Handle))
            TaskbarWidgetNative.ShowWindow(_window.Handle, TaskbarWidgetNative.SwHide);
    }

    /// <summary>Records visibility transitions so shell interactions can be diagnosed later.</summary>
    private static void LogWidgetState(string state)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexQuotaTray");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "TaskbarWidget.log"),
                $"{DateTimeOffset.Now:O} {state}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never affect the tray application.
        }
    }

    /// <summary>Rechecks widget placement when focus changes or a shell menu or window closes.</summary>
    private void OnShellUiChanged(nint hook, uint eventType, nint window, int objectId, int childId,
        uint eventThread, uint eventTime)
    {
        if (_disposed || !_visible || !_dispatchTarget.IsHandleCreated || _dispatchTarget.IsDisposed) return;
        if (eventType == TaskbarWidgetNative.EventObjectHide && (objectId != 0 || childId != 0)) return;
        if (_reattachQueued) return;
        try
        {
            // Let Explorer finish its Z-order change, and combine bursts of hide events into one update.
            _reattachQueued = true;
            _dispatchTarget.BeginInvoke((Action)(() =>
            {
                _reattachQueued = false;
                EnsureAttached();
            }));
        }
        catch (InvalidOperationException)
        {
            _reattachQueued = false; // The handle can disappear during taskbar changes or shutdown.
        }
    }

    /// <summary>Records unexpected widget attachment or painting failures for troubleshooting.</summary>
    private static void LogWidgetError(Exception error)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexQuotaTray");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "TaskbarWidget.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent the tray application from starting.
        }
    }

    /// <summary>Creates the fallback overlay handle using the taskbar's DPI context.</summary>
    private void CreateHandleForTaskbar(nint taskbar)
    {
        var previousContext = TaskbarWidgetNative.SetThreadDpiAwarenessContext(
            TaskbarWidgetNative.GetWindowDpiAwarenessContext(taskbar));
        try { _ = _window.Handle; }
        finally
        {
            if (previousContext != nint.Zero)
                TaskbarWidgetNative.SetThreadDpiAwarenessContext(previousContext);
        }
    }

    /// <summary>Recreates an invalid fallback overlay handle using the current taskbar DPI context.</summary>
    private void RecreateHandleForTaskbar(nint taskbar)
    {
        var previousContext = TaskbarWidgetNative.SetThreadDpiAwarenessContext(
            TaskbarWidgetNative.GetWindowDpiAwarenessContext(taskbar));
        try { _window.RecreateWidgetHandle(); }
        finally
        {
            if (previousContext != nint.Zero)
                TaskbarWidgetNative.SetThreadDpiAwarenessContext(previousContext);
        }
    }

    /// <summary>Hides and disposes both widget windows as the application exits.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TaskbarWidgetNative.StopForegroundMonitoring(_foregroundHook);
        TaskbarWidgetNative.StopForegroundMonitoring(_windowHideHook);
        TaskbarWidgetNative.StopForegroundMonitoring(_menuEndHook);
        _foregroundHook = nint.Zero;
        _windowHideHook = nint.Zero;
        _menuEndHook = nint.Zero;
        HideWindow();
        _embeddedWindow.Dispose();
        _window.Dispose();
    }
}

/// <summary>Draws the two remaining-quota bars and daily-pace marker above the taskbar.</summary>
internal sealed class TaskbarWidgetForm : Form
{
    internal const int LogicalWidth = 300;
    internal const int LogicalHeight = 46;

    private Quota? _quota;
    private Color _dailyPaceColor = Color.Gray;
    private float _renderScale = 1f;

    /// <summary>Initializes a borderless tool window that does not activate when shown.</summary>
    public TaskbarWidgetForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Codex Quota Tray";
        AccessibleName = AppText.Get("Menu.ShowTaskbarWidget");
        DoubleBuffered = true;
        var transparencyKey = ThemeContrastColor.GetWidgetTransparencyKey();
        BackColor = transparencyKey;
        TransparencyKey = transparencyKey;
    }

    /// <summary>Adds tool-window, topmost, and no-activate styles before Windows creates the handle.</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= TaskbarWidgetNative.WsExToolWindow |
                TaskbarWidgetNative.WsExNoActivate | TaskbarWidgetNative.WsExTopmost;
            return parameters;
        }
    }

    /// <summary>Stores a fresh quota sample and redraws the progress bars.</summary>
    public void SetQuota(Quota quota, Color dailyPaceColor)
    {
        _quota = quota;
        _dailyPaceColor = dailyPaceColor;
    }

    /// <summary>Clears the quota sample and redraws the widget in its unavailable state.</summary>
    public void SetUnavailable()
    {
        _quota = null;
        _dailyPaceColor = Color.Gray;
    }

    /// <summary>Recreates the native window handle if Windows destroys it.</summary>
    public void RecreateWidgetHandle() => RecreateHandle();

    /// <summary>Invalidates the overlay so Windows repaints it at the taskbar's DPI.</summary>
    public void RenderTaskbarSurface(uint dpi)
    {
        if (!IsHandleCreated || !TaskbarWidgetNative.IsWindow(Handle)) return;
        _renderScale = dpi / 96f;
        var transparencyKey = ThemeContrastColor.GetWidgetTransparencyKey();
        if (BackColor != transparencyKey) BackColor = transparencyKey;
        if (TransparencyKey != transparencyKey) TransparencyKey = transparencyKey;
        Invalidate();
        Update();
    }

    /// <summary>Paints the widget directly through the window's normal Windows paint cycle.</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawWidget(e.Graphics, _renderScale, ClientSize.Width, ClientSize.Height, TransparencyKey);
    }

    /// <summary>Renders quota content with per-pixel alpha for the taskbar child window.</summary>
    public Bitmap CreateLayeredBitmap(uint dpi, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        DrawWidget(graphics, dpi / 96f, width, height, Color.Transparent);
        return bitmap;
    }

    /// <summary>Draws localized labels, remaining percentages, reset times, bars, and the daily marker.</summary>
    private void DrawWidget(Graphics graphics, float scale, int width, int height, Color background)
    {
        graphics.Clear(background);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var textColor = ThemeContrastColor.GetWidgetTextColor();
        var mutedTextColor = Color.FromArgb(210, textColor);
        var trackColor = Color.FromArgb(68, textColor);
        var accentColor = ThemeContrastColor.GetWeeklyRingColor();
        var fiveHourLabel = AppText.Get("Widget.FiveHourLabel");
        var weeklyLabel = AppText.Get("Widget.WeeklyLabel");
        var unknownPercent = AppText.Get("Widget.UnknownPercent");
        var unknownReset = AppText.Get("Widget.UnknownReset");
        var fiveHourReset = _quota is null || _quota.FiveHourReset == DateTimeOffset.MinValue ? unknownReset :
            _quota.FiveHourReset.ToString("HH:mm", AppText.Culture);
        var weeklyReset = _quota is null || _quota.WeeklyReset == DateTimeOffset.MinValue ? unknownReset :
            _quota.WeeklyReset.ToString(AppText.Get("Tooltip.ResetFormat"), AppText.Culture);

        var dotBounds = new RectangleF(6 * scale, (height - 16 * scale) / 2, 16 * scale, 16 * scale);
        using (var dotOutline = new Pen(textColor, 2.3f * scale))
            graphics.DrawEllipse(dotOutline, dotBounds);
        using (var dotFill = new SolidBrush(_dailyPaceColor))
            graphics.FillEllipse(dotFill, RectangleF.Inflate(dotBounds, -2.2f * scale, -2.2f * scale));

        var barLeft = 63 * scale;
        var barWidth = 92 * scale;
        var percentLeft = 162 * scale;
        var resetLeft = 211 * scale;
        var resetWidth = width - resetLeft - 6 * scale;
        var rowTop = 3 * scale;
        var rowHeight = 17 * scale;
        var rowGap = 5 * scale;
        var fiveHourRemaining = _quota?.FiveHourRemaining;
        var weeklyRemaining = _quota?.WeeklyRemaining;

        DrawQuotaRow(graphics, fiveHourLabel, fiveHourRemaining, fiveHourReset, accentColor,
            barLeft, barWidth, percentLeft, resetLeft, resetWidth, rowTop, rowHeight, scale,
            textColor, mutedTextColor, trackColor, unknownPercent);
        DrawQuotaRow(graphics, weeklyLabel, weeklyRemaining, weeklyReset, accentColor,
            barLeft, barWidth, percentLeft, resetLeft, resetWidth, rowTop + rowHeight + rowGap, rowHeight, scale,
            textColor, mutedTextColor, trackColor, unknownPercent);
    }

    /// <summary>Draws one remaining-quota row with its label, progress bar, percentage, and reset text.</summary>
    private static void DrawQuotaRow(
        Graphics graphics, string label, int? remaining, string reset, Color fillColor,
        float barLeft, float barWidth, float percentLeft, float resetLeft, float resetWidth,
        float top, float height, float scale, Color textColor, Color mutedTextColor,
        Color trackColor, string unknownPercent)
    {
        using var textFont = new Font("Segoe UI", 11.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(textColor);
        using var percentBrush = new SolidBrush(remaining.HasValue ? textColor : mutedTextColor);
        using var format = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };

        graphics.DrawString(label, textFont, textBrush, new RectangleF(28 * scale, top, 32 * scale, height), format);

        var barHeight = 12 * scale;
        var barTop = top + (height - barHeight) / 2;
        using (var trackBrush = new SolidBrush(trackColor))
            FillRoundedRectangle(graphics, trackBrush, new RectangleF(barLeft, barTop, barWidth, barHeight), barHeight / 2);
        if (remaining is int value && value > 0)
        {
            var fillWidth = barWidth * Math.Clamp(value, 0, 100) / 100f;
            using var fillBrush = new SolidBrush(fillColor);
            FillRoundedRectangle(graphics, fillBrush, new RectangleF(barLeft, barTop, fillWidth, barHeight), Math.Min(barHeight / 2, fillWidth / 2));
        }

        var percentText = remaining.HasValue
            ? string.Format(AppText.Culture, "{0} %", Math.Clamp(remaining.Value, 0, 100))
            : unknownPercent;
        graphics.DrawString(percentText, textFont, percentBrush,
            new RectangleF(percentLeft, top, 43 * scale, height), format);
        graphics.DrawString(reset, textFont, textBrush,
            new RectangleF(resetLeft, top, resetWidth, height), format);
    }

    /// <summary>Fills a rounded bar segment while keeping very short remaining bars visible.</summary>
    private static void FillRoundedRectangle(Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var diameter = Math.Min(bounds.Height, radius * 2);
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}

/// <summary>Finds the Windows taskbar and provides native operations for placing the widget.</summary>
internal static class TaskbarWidgetNative
{
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WsExTopmost = 0x00000008;
    internal const int SwpNoActivate = 0x0010;
    internal const int SwpNoMove = 0x0002;
    internal const int SwpNoSize = 0x0001;
    internal const int SwpShowWindow = 0x0040;
    internal const int SwHide = 0;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventSystemMenuEnd = 0x0005;
    internal const uint EventObjectHide = 0x8003;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WinEventSkipOwnProcess = 0x0002;
    internal static readonly nint HwndTop = nint.Zero;
    internal static readonly nint HwndTopmost = new(-1);
    private static nint _widgetsTaskbar;
    private static AutomationElement? _widgetsButton;
    private static int _lastWidgetsLeft;
    private static nint _trayTaskbar;
    private static int _lastTrayLeft;

    /// <summary>Receives foreground-window changes on the application's message-loop thread.</summary>
    internal delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId,
        uint eventThread, uint eventTime);

    /// <summary>Watches shell focus changes so the widget need not wait for the periodic timer.</summary>
    internal static nint WatchForegroundChanges(WinEventProc callback) => SetWinEventHookNative(
        EventSystemForeground, EventSystemForeground, nint.Zero, callback, 0, 0,
        WinEventOutOfContext | WinEventSkipOwnProcess);

    /// <summary>Watches native popup windows closing, including shell surfaces that keep focus.</summary>
    internal static nint WatchWindowHides(WinEventProc callback) => SetWinEventHookNative(
        EventObjectHide, EventObjectHide, nint.Zero, callback, 0, 0,
        WinEventOutOfContext | WinEventSkipOwnProcess);

    /// <summary>Watches menu dismissal when Windows does not move keyboard focus.</summary>
    internal static nint WatchMenuEnds(WinEventProc callback) => SetWinEventHookNative(
        EventSystemMenuEnd, EventSystemMenuEnd, nint.Zero, callback, 0, 0,
        WinEventOutOfContext | WinEventSkipOwnProcess);

    /// <summary>Releases the foreground-event hook during application shutdown.</summary>
    internal static void StopForegroundMonitoring(nint hook)
    {
        if (hook != nint.Zero) UnhookWinEventNative(hook);
    }

    /// <summary>Locates the primary taskbar's native window.</summary>
    internal static nint FindPrimaryTaskbar() => FindWindow("Shell_TrayWnd", null);

    /// <summary>Finds taskbar and notification-area screen bounds used to place the widget.</summary>
    internal static bool TryGetTaskbarGeometry(nint taskbar, out TaskbarGeometry geometry)
    {
        geometry = default;
        if (!GetWindowRect(taskbar, out var taskbarRect)) return false;
        if (_trayTaskbar != taskbar)
        {
            _trayTaskbar = taskbar;
            _lastTrayLeft = 0;
        }

        var trayWindow = FindWindowEx(taskbar, nint.Zero, "TrayNotifyWnd", null);
        if (trayWindow != nint.Zero && GetWindowRect(trayWindow, out var trayRect) &&
            trayRect.Left > taskbarRect.Left && trayRect.Left < taskbarRect.Right)
            _lastTrayLeft = trayRect.Left;

        // Explorer can temporarily remove the tray child while opening or closing Start.
        geometry = new TaskbarGeometry(taskbarRect,
            _lastTrayLeft > taskbarRect.Left && _lastTrayLeft < taskbarRect.Right
                ? _lastTrayLeft : taskbarRect.Right);
        return true;
    }

    /// <summary>Finds the Windows Widgets/weather button by its stable accessibility identifier.</summary>
    internal static bool TryGetWidgetsButtonLeft(nint taskbar, out int left)
    {
        if (_widgetsTaskbar != taskbar)
        {
            _widgetsTaskbar = taskbar;
            _widgetsButton = null;
            _lastWidgetsLeft = 0;
        }

        try
        {
            _widgetsButton ??= AutomationElement.FromHandle(taskbar).FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "WidgetsButton"));
            if (_widgetsButton is not null && !_widgetsButton.Current.IsOffscreen)
            {
                var bounds = _widgetsButton.Current.BoundingRectangle;
                if (!bounds.IsEmpty && bounds.Width > 0)
                    _lastWidgetsLeft = (int)Math.Round(bounds.Left);
            }
        }
        catch (Exception)
        {
            // Weather detection is optional; a transient UI Automation failure must not hide the quota widget.
            _widgetsButton = null;
        }

        left = _lastWidgetsLeft;
        return left > 0;
    }

    /// <summary>Reports whether Windows still recognizes the supplied native window handle.</summary>
    internal static bool IsWindow(nint handle) => handle != nint.Zero && IsWindowNative(handle);

    /// <summary>Returns the current DPI for a taskbar window, or zero when unavailable.</summary>
    internal static uint GetDpiForWindow(nint window) => GetDpiForWindowNative(window);

    /// <summary>Returns the taskbar's DPI awareness context for matching the widget handle.</summary>
    internal static nint GetWindowDpiAwarenessContext(nint window) => GetWindowDpiAwarenessContextNative(window);

    /// <summary>Temporarily changes the current thread's DPI awareness for widget window creation.</summary>
    internal static nint SetThreadDpiAwarenessContext(nint context) =>
        context == nint.Zero ? nint.Zero : SetThreadDpiAwarenessContextNative(context);

    /// <summary>Moves or resizes the overlay without activating it.</summary>
    internal static bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, int flags) =>
        SetWindowPosNative(window, insertAfter, x, y, width, height, flags);

    /// <summary>Shows or hides the widget without giving it keyboard focus.</summary>
    internal static bool ShowWindow(nint window, int command) => ShowWindowNative(window, command);

    /// <summary>Stores the taskbar bounds in screen coordinates and its notification-area left edge.</summary>
    internal readonly record struct TaskbarGeometry(NativeRect Taskbar, int TrayLeft);

    /// <summary>Stores a Win32 rectangle in screen coordinates.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect { public int Left, Top, Right, Bottom; }

    /// <summary>Imports the native lookup for the primary taskbar.</summary>
    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? className, string? windowName);

    /// <summary>Imports the native child-window lookup used to find the notification area.</summary>
    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    /// <summary>Imports the native API that returns a window's screen rectangle.</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    /// <summary>Imports the native API that repositions a window while keeping it inactive.</summary>
    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPosNative(nint window, nint insertAfter, int x, int y, int width, int height, int flags);

    /// <summary>Imports the native API that displays a window without activating it.</summary>
    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowNative(nint window, int command);

    /// <summary>Imports foreground-change notifications from the current desktop.</summary>
    [DllImport("user32.dll", EntryPoint = "SetWinEventHook", SetLastError = true)]
    private static extern nint SetWinEventHookNative(uint eventMin, uint eventMax, nint module,
        WinEventProc callback, uint processId, uint threadId, uint flags);

    /// <summary>Imports removal of the foreground-change notification hook.</summary>
    [DllImport("user32.dll", EntryPoint = "UnhookWinEvent", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEventNative(nint hook);

    /// <summary>Imports the native API that checks whether an HWND remains valid.</summary>
    [DllImport("user32.dll", EntryPoint = "IsWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowNative(nint window);

    /// <summary>Imports the native API that reads a window's effective DPI.</summary>
    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow", SetLastError = true)]
    private static extern uint GetDpiForWindowNative(nint window);

    /// <summary>Imports the native API that reads a window's DPI awareness context.</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowDpiAwarenessContext", SetLastError = true)]
    private static extern nint GetWindowDpiAwarenessContextNative(nint window);

    /// <summary>Imports the native API that temporarily changes the thread's DPI awareness context.</summary>
    [DllImport("user32.dll", EntryPoint = "SetThreadDpiAwarenessContext", SetLastError = true)]
    private static extern nint SetThreadDpiAwarenessContextNative(nint context);

}
