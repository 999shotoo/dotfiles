using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EditorSidebar;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal record struct Rect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential)]
    internal record struct Point(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor, Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData
    {
        internal int Size;
        internal nint Handle;
        internal uint Callback, Edge;
        internal Rect Bounds;
        internal nint Parameter;
    }

    internal record AppWindow(nint Handle, string Title);
    [StructLayout(LayoutKind.Sequential)]
    internal struct ThumbnailProperties
    {
        internal uint Flags;
        internal Rect Destination, Source;
        internal byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] internal bool Visible, ClientOnly;
    }

    private delegate bool Enumerator(nint hwnd, nint param);

    [DllImport("shell32")] internal static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32")] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32")] private static extern bool SystemParametersInfo(uint action, uint parameter, ref Rect rect, uint flags);
    [DllImport("user32")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32")] internal static extern nint GetForegroundWindow();
    [DllImport("user32")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32")] internal static extern bool IsZoomed(nint hwnd);
    [DllImport("user32")] internal static extern int GetWindowRgn(nint hwnd, nint region);
    [DllImport("user32")] internal static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32")] internal static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32")] internal static extern bool EqualRgn(nint first, nint second);
    [DllImport("gdi32")] internal static extern bool DeleteObject(nint region);
    [DllImport("user32")] internal static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32")] internal static extern bool LockWorkStation();
    [DllImport("user32")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("dwmapi")] internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Point size);
    [DllImport("dwmapi")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail, in ThumbnailProperties properties);
    [DllImport("dwmapi")] internal static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, in int value, int size);
    [DllImport("user32")] private static extern bool EnumWindows(Enumerator callback, nint param);
    [DllImport("user32")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int capacity);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int capacity);
    [DllImport("user32", EntryPoint = "GetClassLongPtrW")] private static extern nint GetClassLongPtr(nint hwnd, int index);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint SendMessageTimeout(nint hwnd, uint message,
        nuint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("dwmapi")] private static extern int DwmGetWindowAttribute(nint hwnd, uint attr, out int value, int size);
    [DllImport("dwmapi", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int GetExtendedBounds(nint hwnd, uint attr, out Rect bounds, int size);
    [DllImport("user32")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);

    internal static AppBarData BarData(nint hwnd) => new() { Size = Marshal.SizeOf<AppBarData>(), Handle = hwnd };

    internal static bool SetWorkArea(Rect area) => SystemParametersInfo(0x002F, 0, ref area, 0);

    internal static Rect RemoveHiddenTaskbarInset(Rect monitor, Rect work)
    {
        var shell = FindWindow("Shell_TrayWnd", null);
        if (shell == 0 || IsWindowVisible(shell))
            return work;
        var data = BarData(shell);
        if (SHAppBarMessage(5, ref data) == 0)
            return work;
        var taskbar = data.Bounds;
        return data.Edge switch
        {
            0 when taskbar.Left == monitor.Left && work.Left == taskbar.Right => work with { Left = monitor.Left },
            1 when taskbar.Top == monitor.Top && work.Top == taskbar.Bottom => work with { Top = monitor.Top },
            2 when taskbar.Right == monitor.Right && work.Right == taskbar.Left => work with { Right = monitor.Right },
            3 when taskbar.Bottom == monitor.Bottom && work.Bottom == taskbar.Top => work with { Bottom = monitor.Bottom },
            _ => work
        };
    }

    internal static bool TryGetAppFrame(nint window, out Rect frame)
    {
        frame = default;
        return !IsIconic(window) && IsExternalAppWindow(window) &&
            GetExtendedBounds(window, 9, out frame, Marshal.SizeOf<Rect>()) == 0 &&
            frame.Right > frame.Left && frame.Bottom > frame.Top;
    }

    internal static void SetOverlayStyle(nint hwnd, bool clickThrough)
    {
        const int noActivateToolWindow = 0x08000080;
        const int transparent = 0x20;
        const int appWindow = 0x40000;
        nint style = GetWindowLongPtr(hwnd, -20);
        SetWindowLongPtr(hwnd, -20, (style & ~appWindow) | noActivateToolWindow | (clickThrough ? transparent : 0));
    }

    internal static bool CoversDisplay(nint hwnd, Rect monitor)
    {
        if (hwnd == 0 || IsIconic(hwnd) || IsZoomed(hwnd) || !IsWindowVisible(hwnd) || monitor == default)
            return false;
        if (IsShellSurface(hwnd))
            return false;
        if (GetExtendedBounds(hwnd, 9, out var bounds, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(hwnd, out bounds))
            return false;
        return bounds.Left <= monitor.Left && bounds.Top <= monitor.Top &&
            bounds.Right >= monitor.Right && bounds.Bottom >= monitor.Bottom;
    }

    internal static bool IsActiveWindow(nint window, nint foreground) =>
        foreground != 0 && (window == foreground || GetAncestor(foreground, 3) == window);

    internal static void SetMinimized(nint window, bool minimize)
    {
        if (!ShowWindowAsync(window, minimize ? 6 : 9))
            PostMessage(window, 0x0112, minimize ? 0xF020 : 0xF120, 0);
    }

    internal static int CaptionAction(nint window, Point point)
    {
        if (window == 0 || !GetWindowRect(window, out var bounds) || point.Y < bounds.Top ||
            point.Y > bounds.Top + 60 * Math.Max(96, GetDpiForWindow(window)) / 96 ||
            GetAncestor(WindowFromPoint(point), 2) != window)
            return 0;
        nuint coordinates = (nuint)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16));
        if (SendMessageTimeout(window, 0x0084, 0, (nint)coordinates, 3, 8, out nint hit) == 0)
            return 0;
        return hit switch { 8 => 0xF020, 20 => 0xF060, _ => 0 };
    }

    internal static void RunCaptionAction(nint window, int command) => PostMessage(window, 0x0112, command, 0);

    private static bool IsExternalAppWindow(nint hwnd)
    {
        if (hwnd == 0 || !IsWindowVisible(hwnd))
            return false;
        GetWindowThreadProcessId(hwnd, out uint process);
        if (process is 0 || process == Environment.ProcessId)
            return false;
        long style = GetWindowLongPtr(hwnd, -20);
        if ((style & (0x80 | 0x08000000)) != 0 || ((style & 0x40000) == 0 && GetWindow(hwnd, 4) != 0))
            return false;
        if (DwmGetWindowAttribute(hwnd, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        return !IsShellSurface(hwnd);
    }

    private static bool IsShellSurface(nint hwnd)
    {
        var windowClass = new StringBuilder(128);
        GetClassName(hwnd, windowClass, windowClass.Capacity);
        return windowClass.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "XamlExplorerHostIslandWindow" or "MultitaskingViewFrame" or "ForegroundStaging"
            or "TaskSwitcherWnd" or "TaskSwitcherOverlayWnd";
    }

    internal static List<AppWindow> Windows()
    {
        var windows = new List<AppWindow>();
        var title = new StringBuilder(512);
        EnumWindows((hwnd, _) =>
        {
            if (IsExternalAppWindow(hwnd) && GetWindowText(hwnd, title, title.Capacity) > 0)
                windows.Add(new(hwnd, title.ToString()));
            return true;
        }, 0);
        return windows.OrderBy(window => window.Handle).ToList();
    }

    internal static ImageSource? WindowIcon(nint hwnd)
    {
        SendMessageTimeout(hwnd, 0x007F, 2, 0, 2, 25, out nint icon);
        if (icon == 0)
            icon = GetClassLongPtr(hwnd, -34);
        if (icon == 0)
            icon = GetClassLongPtr(hwnd, -14);
        if (icon == 0)
            return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            image.Freeze();
            return image;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static void Shortcut(byte key)
    {
        const byte windows = 0x5B;
        keybd_event(windows, 0, 1, 0);
        keybd_event(key, 0, 0, 0);
        keybd_event(key, 0, 2, 0);
        keybd_event(windows, 0, 3, 0);
    }
}
