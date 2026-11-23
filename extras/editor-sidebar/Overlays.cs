using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace EditorSidebar;

internal sealed class ScreenFrame : Window
{
    internal const int FadeMilliseconds = 100;
    private readonly Grid drawing = new() { IsHitTestVisible = false };
    private readonly Path mask = new() { Fill = new SolidColorBrush(Color.FromRgb(38, 38, 38)) };
    private readonly Path rim = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(179, 162, 130)), StrokeThickness = 1, Opacity = 0
    };
    private nint handle;
    private Native.Rect frameBounds;
    private Native.Rect rimBounds;
    private double railWidth, outerInset;
    internal bool RimVisible { get; private set; }

    internal ScreenFrame()
    {
        Title = "Editor Sidebar Frame";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        Content = drawing;
        drawing.Children.Add(mask);
        drawing.Children.Add(rim);
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            Native.SetOverlayStyle(handle, true);
        };
        SizeChanged += (_, _) => Redraw();
    }

    internal void Place(Native.Rect bounds, double leftRailWidth, double inset, nint rail)
    {
        bool show = !IsVisible;
        bool geometryChanged = railWidth != leftRailWidth || outerInset != inset || frameBounds != bounds;
        frameBounds = bounds;
        railWidth = leftRailWidth;
        outerInset = inset;
        if (show)
            Show();
        if (show || !Native.GetWindowRect(handle, out var current) || current != bounds)
            Native.SetWindowPos(handle, rail, bounds.Left, bounds.Top,
                bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0010);
        if (geometryChanged)
            Redraw();
    }

    internal bool CanShowRim(Native.Rect bounds)
    {
        var area = ToLocal(bounds);
        return area.Left >= railWidth && area.Top >= outerInset &&
            area.Right <= drawing.ActualWidth - outerInset && area.Bottom <= drawing.ActualHeight - outerInset &&
            area.Width >= 48 && area.Height >= 48;
    }

    internal void ShowRim(Native.Rect? bounds)
    {
        bool visible = bounds.HasValue;
        if (RimVisible == visible)
            return;
        if (bounds is { } next)
        {
            rimBounds = next;
            DrawRim();
        }
        RimVisible = visible;
        double from = rim.Opacity;
        double to = visible ? 1 : 0;
        rim.BeginAnimation(OpacityProperty, null);
        rim.Opacity = to;
        if (SystemParameters.MinimizeAnimation)
            rim.BeginAnimation(OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(FadeMilliseconds))
            {
                FillBehavior = FillBehavior.Stop
            });
    }

    private void Redraw()
    {
        double width = drawing.ActualWidth;
        double height = drawing.ActualHeight;
        if (width <= railWidth + outerInset || height <= 2 * outerInset)
            return;
        var screen = new RectangleGeometry(new Rect(0, 0, width, height));
        var opening = new RectangleGeometry(new Rect(railWidth, outerInset,
            width - railWidth - outerInset, height - 2 * outerInset), 15, 15);
        mask.Data = new CombinedGeometry(GeometryCombineMode.Exclude, screen, opening);
        DrawRim();
    }

    private Rect ToLocal(Native.Rect bounds)
    {
        var dpi = VisualTreeHelper.GetDpi(drawing);
        return new Rect((bounds.Left - frameBounds.Left) / dpi.DpiScaleX,
            (bounds.Top - frameBounds.Top) / dpi.DpiScaleY,
            (bounds.Right - bounds.Left) / dpi.DpiScaleX, (bounds.Bottom - bounds.Top) / dpi.DpiScaleY);
    }

    private void DrawRim()
    {
        var area = ToLocal(rimBounds);
        if (area.Width < 48 || area.Height < 48)
            return;
        const double inset = 0.5;
        rim.Data = new RectangleGeometry(new Rect(area.Left + inset, area.Top + inset,
            area.Width - 2 * inset, area.Height - 2 * inset), MaximizedCorners.Radius, MaximizedCorners.Radius);
    }
}

internal sealed class WindowPreview : Window
{
    private readonly Border viewport = new();
    private readonly TextBlock fallback = new() { Text = "Preview unavailable", HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center };
    private nint thumbnail;

    internal WindowPreview(Native.AppWindow app, Button anchor, Brush background, Brush foreground, Native.Rect work)
    {
        Title = "Window Preview";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = background;
        Foreground = foreground;
        Width = 258;
        Height = 175;
        viewport.Background = new SolidColorBrush(Color.FromRgb(30, 28, 27));
        viewport.Child = fallback;
        var layout = new DockPanel { Margin = new Thickness(8) };
        var title = new TextBlock { Text = app.Title, TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12, Margin = new Thickness(3, 0, 3, 7) };
        DockPanel.SetDock(title, Dock.Top);
        layout.Children.Add(title);
        layout.Children.Add(viewport);
        Content = layout;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.SetOverlayStyle(hwnd, true);
            Native.DwmSetWindowAttribute(hwnd, 33, 2, sizeof(int));
            var point = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight / 2));
            double scale = (Native.GetDpiForWindow(hwnd) is uint dpi && dpi > 0 ? dpi : 96) / 96.0;
            int pixelWidth = (int)Math.Round(Width * scale);
            int pixelHeight = (int)Math.Round(Height * scale);
            int x = Math.Clamp((int)point.X + 12, work.Left + 2, Math.Max(work.Left + 2, work.Right - pixelWidth - 8));
            int y = Math.Clamp((int)point.Y - pixelHeight / 2, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - pixelHeight - 8));
            Native.SetWindowPos(hwnd, 0, x, y, pixelWidth, pixelHeight, 0x0014);
            if (Native.DwmRegisterThumbnail(hwnd, app.Handle, out thumbnail) != 0)
                thumbnail = 0;
        };
        Loaded += (_, _) => UpdateThumbnail();
        viewport.SizeChanged += (_, _) => UpdateThumbnail();
        Closed += (_, _) =>
        {
            if (thumbnail != 0)
                Native.DwmUnregisterThumbnail(thumbnail);
        };
    }

    private void UpdateThumbnail()
    {
        if (thumbnail == 0 || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0 ||
            Native.DwmQueryThumbnailSourceSize(thumbnail, out var size) != 0 || size.X <= 0 || size.Y <= 0)
            return;

        var origin = viewport.TranslatePoint(new Point(), this);
        var dpi = VisualTreeHelper.GetDpi(viewport);
        double width = viewport.ActualWidth * dpi.DpiScaleX;
        double height = viewport.ActualHeight * dpi.DpiScaleY;
        double ratio = Math.Min(width / size.X, height / size.Y);
        int left = (int)(origin.X * dpi.DpiScaleX + (width - size.X * ratio) / 2);
        int top = (int)(origin.Y * dpi.DpiScaleY + (height - size.Y * ratio) / 2);
        var properties = new Native.ThumbnailProperties
        {
            Flags = 1 | 4 | 8, Destination = new(left, top, left + (int)(size.X * ratio), top + (int)(size.Y * ratio)),
            Opacity = 255, Visible = true
        };
        if (Native.DwmUpdateThumbnailProperties(thumbnail, in properties) == 0)
            fallback.Visibility = Visibility.Collapsed;
    }
}
