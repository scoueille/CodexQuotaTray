using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CodexQuotaTray;

/// <summary>Displays quota pixels as a layered child of Explorer's primary taskbar.</summary>
internal sealed class EmbeddedTaskbarWidget : NativeWindow, IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsChild = 0x40000000;
    private const int WsClipSiblings = 0x04000000;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int SwpNoActivate = 0x0010;
    private const int SwpFrameChanged = 0x0020;
    private const int SwShowNoActivate = 4;
    private const int SwHide = 0;
    private const uint UlwAlpha = 0x00000002;
    private static readonly nint HwndTop = nint.Zero;
    private nint _taskbar;

    /// <summary>Creates or reuses the taskbar child, positions it, and supplies transparent pixels.</summary>
    public bool TryShow(nint taskbar, TaskbarWidgetNative.NativeRect taskbarRect,
        int screenX, int screenY, int width, int height, Bitmap bitmap, out string error)
    {
        error = string.Empty;
        if (!EnsureChild(taskbar, out error)) return false;

        var x = screenX - taskbarRect.Left;
        var y = screenY - taskbarRect.Top;
        if (!SetWindowPos(Handle, HwndTop, x, y, width, height,
                SwpNoActivate | SwpFrameChanged))
        {
            error = $"position failed: {Marshal.GetLastWin32Error()}";
            ResetHandle();
            return false;
        }

        if (!Render(bitmap, out error))
        {
            ResetHandle();
            return false;
        }

        ShowWindow(Handle, SwShowNoActivate);
        return true;
    }

    /// <summary>Hides the child when the user disables the widget or placement is unavailable.</summary>
    public void Hide()
    {
        if (Handle != nint.Zero && TaskbarWidgetNative.IsWindow(Handle))
            ShowWindow(Handle, SwHide);
    }

    /// <summary>Releases the native child window and its Explorer parent relationship.</summary>
    public void Dispose() => ResetHandle();

    /// <summary>Creates a layered popup and converts it to a child of the current taskbar.</summary>
    private bool EnsureChild(nint taskbar, out string error)
    {
        error = string.Empty;
        if (Handle != nint.Zero && (_taskbar != taskbar || !TaskbarWidgetNative.IsWindow(Handle)))
            ResetHandle();
        if (Handle != nint.Zero) return true;

        CreateHandle(new CreateParams
        {
            Caption = "Codex Quota Tray widget",
            Style = WsPopup,
            ExStyle = WsExLayered | WsExToolWindow | WsExNoActivate,
            X = 0,
            Y = 0,
            Width = 1,
            Height = 1
        });
        if (Handle == nint.Zero)
        {
            error = "layered window creation failed";
            return false;
        }

        var style = GetWindowLong(Handle, -16);
        SetWindowLong(Handle, -16, (style & ~WsPopup) | WsChild | WsClipSiblings);
        SetParent(Handle, taskbar);
        if (GetParent(Handle) != taskbar)
        {
            error = $"taskbar parenting failed: {Marshal.GetLastWin32Error()}";
            ResetHandle();
            return false;
        }

        _taskbar = taskbar;
        return true;
    }

    /// <summary>Copies premultiplied bitmap pixels into a DIB and updates the layered child.</summary>
    private bool Render(Bitmap bitmap, out string error)
    {
        error = string.Empty;
        var screenDc = GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            error = $"screen DC unavailable: {Marshal.GetLastWin32Error()}";
            return false;
        }

        nint memoryDc = nint.Zero;
        nint dib = nint.Zero;
        nint previousBitmap = nint.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == nint.Zero)
            {
                error = $"memory DC unavailable: {Marshal.GetLastWin32Error()}";
                return false;
            }

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = bitmap.Width,
                    Height = -bitmap.Height,
                    Planes = 1,
                    BitCount = 32,
                    SizeImage = (uint)(bitmap.Width * bitmap.Height * 4)
                }
            };
            dib = CreateDIBSection(screenDc, ref info, 0, out var pixels, nint.Zero, 0);
            if (dib == nint.Zero || pixels == nint.Zero)
            {
                error = $"32-bit DIB creation failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[bitmap.Width * 4];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(pixels, y * row.Length), row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            previousBitmap = SelectObject(memoryDc, dib);
            if (previousBitmap == nint.Zero)
            {
                error = $"DIB selection failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            var size = new NativeSize { Width = bitmap.Width, Height = bitmap.Height };
            var source = new NativePoint();
            var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(Handle, screenDc, nint.Zero, ref size,
                    memoryDc, ref source, 0, ref blend, UlwAlpha))
            {
                error = $"layered drawing failed: {Marshal.GetLastWin32Error()}";
                return false;
            }
            return true;
        }
        finally
        {
            if (previousBitmap != nint.Zero) SelectObject(memoryDc, previousBitmap);
            if (dib != nint.Zero) DeleteObject(dib);
            if (memoryDc != nint.Zero) DeleteDC(memoryDc);
            ReleaseDC(nint.Zero, screenDc);
        }
    }

    /// <summary>Destroys a live child or releases a handle already destroyed by Explorer.</summary>
    private void ResetHandle()
    {
        if (Handle == nint.Zero) return;
        if (TaskbarWidgetNative.IsWindow(Handle)) DestroyHandle();
        else ReleaseHandle();
        _taskbar = nint.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ImportantColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint FirstColor;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetParent", SetLastError = true)]
    private static extern nint SetParent(nint child, nint parent);

    [DllImport("user32.dll", EntryPoint = "GetParent", SetLastError = true)]
    private static extern nint GetParent(nint window);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter,
        int x, int y, int width, int height, int flags);

    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", EntryPoint = "GetDC", SetLastError = true)]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC", SetLastError = true)]
    private static extern int ReleaseDC(nint window, nint dc);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll", EntryPoint = "DeleteDC", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", SetLastError = true)]
    private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info,
        uint usage, out nint pixels, nint section, uint offset);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject", SetLastError = true)]
    private static extern nint SelectObject(nint dc, nint bitmap);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint bitmap);

    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(nint window, nint destinationDc,
        nint destinationPosition, ref NativeSize size, nint sourceDc,
        ref NativePoint sourcePosition, uint colorKey, ref BlendFunction blend, uint flags);
}
