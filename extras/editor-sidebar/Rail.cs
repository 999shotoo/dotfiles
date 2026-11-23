using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EditorSidebar;

internal sealed class Rail : Window
{
    private const int RailWidth = 48;
    private const int WindowGap = 12;
    private const int OuterInset = 14;
    private static readonly Brush Ink = Paint("#262626");
    private static readonly Brush Surface = Paint("#393633");
    private static readonly Brush Cream = Paint("#D8C9A5");
    private static readonly Brush Olive = Paint("#A8A34B");
    private static readonly Brush Rose = Paint("#C47788");
    private readonly StackPanel apps = new();
    private readonly TextBlock clock = new() { TextAlignment = TextAlignment.Center, Foreground = Cream };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer fullscreenTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer appsTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Dictionary<nint, ImageSource?> icons = new();
    private readonly MaximizedCorners maximizedCorners = new();
    private nint handle;
    private bool repositionQueued;
    private Native.Rect bounds;
    private Popup? popup;
    private nint popupForeground;
    private List<Native.AppWindow> lastWindows = new();
    private nint lastActive;
    private int lastLimit;
    private ScreenFrame? frame;
    private WindowPreview? preview;
    private Button? hoverAnchor;
    private Native.AppWindow? hoveredApp;
    private Native.Rect frameBounds;
    private Native.Rect monitorBounds;
    private Native.Rect originalWorkArea;
    private Native.Rect appliedWorkArea;
    private bool fullscreen;
    private bool pendingFullscreen;
    private bool overflowMenu;
    private nint outlineWindow;
    private Native.Rect outlineBounds;
    private long outlineSince;
    private nint minimizingWithRim;
    private CaptionFade? captionFade;
    private nint captionWindow;
    private long captionStarted;
    private nint nativeMinimizeWindow;
    private long nativeMinimizeStarted;
    private bool exiting;

    [STAThread]
    public static void Main()
    {
        using var instance = new Mutex(true, @"Local\EditorSidebar", out bool first);
        if (first)
            new Application { ShutdownMode = ShutdownMode.OnMainWindowClose }.Run(new Rail());
    }

    private Rail()
    {
        Title = "Editor Sidebar";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = RailWidth;
        Height = 480;
        UseLayoutRounding = true;
        Content = BuildLayout();
        SourceInitialized += (_, _) => InitializeBar();
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(PositionBar, DispatcherPriority.ApplicationIdle);
            Refresh();
            timer.Start();
            fullscreenTimer.Start();
            appsTimer.Start();
            RefreshApps();
            captionFade = new CaptionFade(
                window => SystemParameters.MinimizeAnimation && window != 0 && window == outlineWindow &&
                    captionWindow == 0 && frame?.RimVisible == true,
                StartCaptionFade, FinishCaptionFade, OnNativeMinimize);
        };
        timer.Tick += (_, _) => Refresh();
        hoverTimer.Tick += (_, _) => ShowPreview();
        fullscreenTimer.Tick += (_, _) => UpdateFullscreen();
        appsTimer.Tick += (_, _) => RefreshApps();
        Closed += (_, _) =>
        {
            exiting = true;
            captionFade?.Dispose();
            timer.Stop();
            fullscreenTimer.Stop();
            appsTimer.Stop();
            DismissPreview();
            if (popup != null)
                popup.IsOpen = false;
            maximizedCorners.Clear();
            frame?.Close();
            if (ReadMonitor(out var monitor) && monitor.Work == appliedWorkArea)
                Native.SetWorkArea(originalWorkArea);
        };
    }

    private Border BuildLayout()
    {
        var layout = new Grid();
        layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });

        var top = new StackPanel();
        top.Children.Add(IconButton("\uE721", "Search apps", () => Native.Shortcut(0x53)));
        top.Children.Add(Pill(apps));
        layout.Children.Add(top);

        var desktops = IconButton("\uE7C4", "Task View and virtual desktops", () => Native.Shortcut(0x09));
        Grid.SetRow(desktops, 1);
        layout.Children.Add(desktops);

        var bottom = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        var calendar = IconButton("\uE787", "Calendar");
        calendar.Click += (_, _) => Open(calendar, () => QuickWidgets.Calendar(Cream, Surface, Olive));
        bottom.Children.Add(calendar);
        clock.FontFamily = new("Consolas");
        clock.FontSize = 12;
        bottom.Children.Add(clock);
        var system = new StackPanel();
        var sound = IconButton("\uE767", "Volume");
        sound.Click += (_, _) => Open(sound, () => QuickWidgets.Volume(Cream, Surface, Olive));
        system.Children.Add(sound);
        var network = IconButton("\uE701", "Network controls");
        network.Click += (_, _) => Open(network,
            () => QuickWidgets.Network(Cream, Surface, () =>
            {
                if (popup != null)
                    popup.IsOpen = false;
                Native.Shortcut(0x41);
            }));
        system.Children.Add(network);
        system.Children.Add(IconButton("\uE7E7", "Notifications", () => Native.Shortcut(0x4E)));
        bottom.Children.Add(Pill(system));
        var power = IconButton("\uE7E8", "Power and sidebar options", foreground: Rose);
        power.Click += (_, _) => PowerMenu(power);
        bottom.Children.Add(power);
        Grid.SetRow(bottom, 2);
        layout.Children.Add(bottom);

        return new Border
        {
            Margin = new Thickness(7, 8, 5, 8), Padding = new Thickness(0, 3, 0, 3), Child = layout,
            Background = Ink, CornerRadius = new(10)
        };
    }

    private static Brush Paint(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static Border Pill(StackPanel child) => new()
    {
        Background = Surface, CornerRadius = new(10), Margin = new Thickness(0, 3, 0, 3), Child = child
    };

    private static Button IconButton(string glyph, string label, Action? action = null, Brush? foreground = null) =>
        ButtonFor(new TextBlock
        {
            Text = glyph, FontFamily = new("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14, Foreground = foreground ?? Cream
        }, label, action);

    private static Button ButtonFor(object content, string label, Action? action, bool active = false)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = new(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(OpacityProperty, 0.72));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
        template.Triggers.Add(focus);

        var button = new Button
        {
            Content = content, ToolTip = Tip(label), Height = 27, MinWidth = 29,
            Background = active ? Olive : Brushes.Transparent, Template = template
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        ToolTipService.SetInitialShowDelay(button, 450);
        if (action != null)
            button.Click += (_, _) => action();
        return button;
    }

    private static ToolTip Tip(string text) => new()
    {
        Placement = PlacementMode.Right, HorizontalOffset = 10,
        Template = TooltipTemplate,
        Content = new TextBlock
        {
            Text = text, Foreground = Cream, FontSize = 12,
            MaxWidth = 240, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 6, 10, 6)
        }
    };

    private static readonly ControlTemplate TooltipTemplate = CreateTooltipTemplate();

    private static ControlTemplate CreateTooltipTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Ink);
        border.SetValue(Border.BorderBrushProperty, Surface);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return new ControlTemplate(typeof(ToolTip)) { VisualTree = border };
    }

    private void InitializeBar()
    {
        handle = new WindowInteropHelper(this).Handle;
        Native.SetOverlayStyle(handle, false);
        HwndSource.FromHwnd(handle).AddHook(WindowMessage);
    }

    private void PositionBar()
    {
        if (handle == 0 || !ReadMonitor(out var info))
            return;
        if (info.Monitor != monitorBounds || info.Work != appliedWorkArea)
            originalWorkArea = info.Work;
        monitorBounds = info.Monitor;
        frameBounds = Native.RemoveHiddenTaskbarInset(info.Monitor, originalWorkArea);
        uint dpi = Native.GetDpiForWindow(handle);
        double scale = (dpi == 0 ? 96 : dpi) / 96.0;
        int rail = (int)Math.Round(RailWidth * scale);
        int leftInset = (int)Math.Round((RailWidth + WindowGap) * scale);
        int edgeInset = (int)Math.Round((OuterInset + WindowGap) * scale);
        var area = new Native.Rect(frameBounds.Left + leftInset, frameBounds.Top + edgeInset,
            frameBounds.Right - edgeInset, frameBounds.Bottom - edgeInset);
        if (area.Right <= area.Left || area.Bottom <= area.Top)
            return;
        if (info.Work == area || Native.SetWorkArea(area))
            appliedWorkArea = area;

        bounds = frameBounds with { Right = frameBounds.Left + rail };
        if (Native.GetWindowRect(handle, out var current) && current != bounds)
            Native.SetWindowPos(handle, 0, bounds.Left, bounds.Top,
                bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0014);
        if (!fullscreen)
        {
            frame ??= new ScreenFrame();
            frame.Place(frameBounds, RailWidth, OuterInset, handle);
        }
    }

    private static bool ReadMonitor(out Native.MonitorInfo info)
    {
        var monitor = Native.MonitorFromPoint(default, 1);
        info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        return Native.GetMonitorInfo(monitor, ref info);
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((message is 0x007E or 0x02E0) && !repositionQueued)
        {
            repositionQueued = true;
            Dispatcher.BeginInvoke(() =>
            {
                repositionQueued = false;
                PositionBar();
            }, DispatcherPriority.Background);
        }
        if (message == 0x0021)
        {
            handled = true;
            return 3;
        }
        return 0;
    }

    private void UpdateFullscreen()
    {
        nint active = Native.GetForegroundWindow();
        bool coversDisplay = Native.CoversDisplay(active, monitorBounds);
        if (coversDisplay == pendingFullscreen)
            SetFullscreen(coversDisplay);
        pendingFullscreen = coversDisplay;
        Native.Rect appBounds = default;
        bool nativeMinimize = active == nativeMinimizeWindow &&
            Stopwatch.GetElapsedTime(nativeMinimizeStarted) < TimeSpan.FromMilliseconds(350);
        nint candidate = !fullscreen && active != minimizingWithRim && active != captionWindow && !nativeMinimize &&
            Native.TryGetAppFrame(active, out appBounds) && frame?.CanShowRim(appBounds) == true ? active : 0;
        if (candidate == 0)
            appBounds = default;
        if (candidate != outlineWindow || appBounds != outlineBounds)
        {
            outlineWindow = candidate;
            outlineBounds = appBounds;
            outlineSince = Stopwatch.GetTimestamp();
            frame?.ShowRim(null);
        }
        if (candidate != 0 && (!SystemParameters.MinimizeAnimation ||
            Stopwatch.GetElapsedTime(outlineSince) >= TimeSpan.FromMilliseconds(180)))
        {
            maximizedCorners.Apply(candidate, appBounds);
            frame?.ShowRim(appBounds);
        }
        if (popup?.IsOpen == true && active != 0 && active != popupForeground &&
            active != handle && active != PresentationSourceHandle(popup.Child))
            popup.IsOpen = false;
    }

    private void StartCaptionFade(nint window)
    {
        captionWindow = window;
        captionStarted = Stopwatch.GetTimestamp();
        frame?.ShowRim(null);
    }

    private void FinishCaptionFade(nint window, int command)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (command == 0)
                    return;
                var remaining = TimeSpan.FromMilliseconds(ScreenFrame.FadeMilliseconds) -
                    Stopwatch.GetElapsedTime(captionStarted);
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining);
                if (!exiting && Native.IsWindow(window))
                    Native.RunCaptionAction(window, command);
            }
            finally
            {
                if (captionWindow == window)
                    captionWindow = 0;
            }
        });
    }

    private void OnNativeMinimize(nint window)
    {
        if (window == 0 || (window != outlineWindow && window != captionWindow))
            return;
        nativeMinimizeWindow = window;
        nativeMinimizeStarted = Stopwatch.GetTimestamp();
        frame?.ShowRim(null);
    }

    private static nint PresentationSourceHandle(UIElement child) =>
        (PresentationSource.FromVisual(child) as HwndSource)?.Handle ?? 0;

    private void SetFullscreen(bool hidden)
    {
        if (fullscreen == hidden || !IsLoaded)
            return;
        fullscreen = hidden;
        if (Content is FrameworkElement layout)
            layout.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        if (hidden)
        {
            DismissPreview();
            if (popup != null)
                popup.IsOpen = false;
            frame?.Hide();
        }
        Native.SetWindowPos(handle, hidden ? -2 : -1, 0, 0, 0, 0, 0x0013);
        if (!hidden)
            PositionBar();
    }

    private void Refresh()
    {
        PositionBar();
        maximizedCorners.Refresh();
        var now = DateTime.Now;
        clock.Text = now.ToString("HH'\n'mm", CultureInfo.InvariantCulture);
        if (clock.ToolTip is ToolTip { Content: TextBlock date })
            date.Text = now.ToLongDateString();
        else
            clock.ToolTip = Tip(now.ToLongDateString());
    }

    private void RefreshApps()
    {
        var windows = Native.Windows();
        nint active = Native.GetForegroundWindow();
        active = windows.Find(window => Native.IsActiveWindow(window.Handle, active))?.Handle ?? 0;
        int limit = Math.Clamp((int)((ActualHeight / 2 - 67) / 27) - 1, 1, 18);
        if (limit == lastLimit && active == lastActive && windows.SequenceEqual(lastWindows))
            return;
        if (limit == lastLimit && windows.SequenceEqual(lastWindows))
        {
            foreach (var button in apps.Children.OfType<Button>())
                button.Background = button.Tag is nint hwnd && hwnd == active ? Olive : Brushes.Transparent;
            lastActive = active;
            return;
        }
        DismissPreview();
        if (overflowMenu && popup != null)
            popup.IsOpen = false;
        lastWindows = windows;
        lastLimit = limit;
        lastActive = active;
        foreach (var stale in icons.Keys.Except(windows.Select(w => w.Handle)).ToArray())
            icons.Remove(stale);
        apps.Children.Clear();
        foreach (var window in windows.Take(limit))
        {
            if (!icons.TryGetValue(window.Handle, out var icon))
                icons[window.Handle] = icon = Native.WindowIcon(window.Handle);
            UIElement content = icon == null
                ? new TextBlock { Text = StringInfo.GetNextTextElement(window.Title), FontSize = 13, Foreground = Cream }
                : new Image { Source = icon, Width = 16, Height = 16 };
            var button = ButtonFor(content, window.Title, null, window.Handle == active);
            button.Click += async (_, _) => await ToggleWindow(window.Handle);
            button.Tag = window.Handle;
            button.ToolTip = null;
            button.Focusable = false;
            button.MouseEnter += (_, _) =>
            {
                DismissPreview();
                hoverAnchor = button;
                hoveredApp = window;
                hoverTimer.Start();
            };
            button.MouseLeave += (_, _) => DismissPreview();
            apps.Children.Add(button);
        }
        if (windows.Count > limit)
        {
            var more = IconButton("\uE712", "More running apps");
            more.Click += (_, _) =>
            {
                var items = new StackPanel();
                foreach (var window in lastWindows.Skip(lastLimit))
                    items.Children.Add(MenuButton(window.Title, async () => await ToggleWindow(window.Handle)));
                Open(more, () => new ScrollViewer { Content = items, MaxHeight = 360, Width = 260,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                overflowMenu = popup?.IsOpen == true;
            };
            apps.Children.Add(more);
        }
    }

    private void ShowPreview()
    {
        hoverTimer.Stop();
        if (fullscreen || popup?.IsOpen == true || hoverAnchor is not { IsMouseOver: true } ||
            hoveredApp is not { } app || !Native.IsWindow(app.Handle))
            return;
        preview = new WindowPreview(app, hoverAnchor, Surface, Cream,
            frameBounds with { Left = bounds.Right });
        preview.Show();
    }

    private void DismissPreview()
    {
        hoverTimer.Stop();
        preview?.Close();
        preview = null;
        hoverAnchor = null;
        hoveredApp = null;
    }

    private async Task ToggleWindow(nint window)
    {
        DismissPreview();
        if (!Native.IsWindow(window) || minimizingWithRim == window)
            return;
        nint active = Native.GetForegroundWindow();
        bool minimize = !Native.IsIconic(window) && Native.IsActiveWindow(window, active);
        if (minimize && frame?.RimVisible == true && outlineWindow == active)
        {
            minimizingWithRim = window;
            outlineWindow = 0;
            frame.ShowRim(null);
            try
            {
                if (SystemParameters.MinimizeAnimation)
                    await Task.Delay(ScreenFrame.FadeMilliseconds);
                if (!exiting && Native.IsWindow(window))
                    SetWindowState(window, true);
            }
            finally
            {
                if (minimizingWithRim == window)
                    minimizingWithRim = 0;
            }
            return;
        }
        SetWindowState(window, minimize);
    }

    private static void SetWindowState(nint window, bool minimize)
    {
        if (minimize)
        {
            Native.SetMinimized(window, true);
            return;
        }
        if (Native.IsIconic(window))
            Native.SetMinimized(window, false);
        Native.SetForegroundWindow(window);
    }

    private Button MenuButton(string label, Action action) => ButtonFor(new TextBlock
    {
        Text = label, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Cream,
        FontSize = 13, Margin = new Thickness(10, 0, 10, 0), MaxWidth = 230
    }, label, () =>
    {
        if (popup != null)
            popup.IsOpen = false;
        action();
    });

    private void Open(Button anchor, Func<UIElement> createContent)
    {
        DismissPreview();
        bool toggledOff = popup?.IsOpen == true && popup.PlacementTarget == anchor;
        if (popup?.IsOpen == true)
            popup.IsOpen = false;
        if (toggledOff)
            return;

        var menu = new Popup
        {
            PlacementTarget = anchor, Placement = PlacementMode.Right, HorizontalOffset = 7,
            AllowsTransparency = true, StaysOpen = true
        };
        var panel = new DockPanel();
        var close = IconButton("\uE711", "Close panel", () => menu.IsOpen = false);
        close.Width = 26;
        close.MinWidth = 0;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(close, Dock.Top);
        panel.Children.Add(close);
        panel.Children.Add(createContent());
        menu.Child = new Border { Child = panel, Background = Ink, BorderBrush = Surface,
            BorderThickness = new Thickness(1), CornerRadius = new(12), Padding = new Thickness(13, 6, 13, 13) };
        menu.Child.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
                menu.IsOpen = false;
        };
        popup = menu;
        popupForeground = Native.GetForegroundWindow();
        overflowMenu = false;
        menu.Closed += (_, _) =>
        {
            if (popup == menu)
                popup = null;
        };
        menu.IsOpen = true;
    }

    private void PowerMenu(Button anchor)
    {
        var items = new StackPanel();
        items.Children.Add(MenuButton("Lock", () => Native.LockWorkStation()));
        items.Children.Add(MenuButton("Restart", () => ConfirmPower("Restart", "/r /t 0")));
        items.Children.Add(MenuButton("Shut down", () => ConfirmPower("Shut down", "/s /t 0")));
        items.Children.Add(MenuButton("Exit sidebar", Close));
        Open(anchor, () => items);
    }

    private void ConfirmPower(string label, string arguments)
    {
        if (MessageBox.Show(this, $"{label} Windows now?", label, MessageBoxButton.YesNo,
                MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            Launch("shutdown.exe", arguments);
    }

    private void Launch(string command, string arguments = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo(command, arguments) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            MessageBox.Show(this, error.Message, Title);
        }
    }
}
