using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EditorSidebar;

internal static class QuickWidgets
{
    internal static StackPanel Volume(Brush foreground, Brush surface, Brush accent)
    {
        var panel = new StackPanel { Width = 220 };
        panel.Children.Add(Heading("Output volume", foreground));
        AudioEndpoint? audio = AudioEndpoint.Open();
        if (audio == null)
        {
            panel.Children.Add(Text("No audio output available", foreground));
            return panel;
        }

        panel.Unloaded += (_, _) => audio.Dispose();
        var slider = new Slider { Maximum = 100, Value = audio.Level * 100, Margin = new Thickness(0, 5, 0, 9),
            IsMoveToPointEnabled = true, Template = VolumeTrack(surface, accent) };
        System.Windows.Automation.AutomationProperties.SetName(slider, "Output volume");
        var meter = Text($"{slider.Value:0}%", foreground);
        meter.FontSize = 26;
        panel.Children.Add(meter);
        slider.ValueChanged += (_, _) =>
        {
            audio.Level = (float)(slider.Value / 100);
            meter.Text = $"{slider.Value:0}%";
        };
        panel.Children.Add(slider);
        var mute = ActionButton(audio.Muted ? "Unmute" : "Mute", foreground, surface);
        mute.Margin = new Thickness(0, 2, 5, 0);
        mute.Click += (_, _) =>
        {
            audio.Muted = !audio.Muted;
            mute.Content = audio.Muted ? "Unmute" : "Mute";
        };
        panel.Children.Add(mute);
        return panel;
    }

    internal static StackPanel Network(Brush foreground, Brush surface, Action openQuickSettings)
    {
        var panel = new StackPanel { Width = 220 };
        panel.Children.Add(Heading("Network", foreground));
        try
        {
            var connections = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(adapter => (adapter, addresses: adapter.GetIPProperties().UnicastAddresses))
                .Where(item => item.addresses.Any(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                .Take(4).ToArray();
            var status = Text(connections.Length == 0 ? "Disconnected" : "Connected", foreground);
            status.FontSize = 18;
            panel.Children.Add(status);
            foreach (var (adapter, _) in connections)
            {
                panel.Children.Add(Text(adapter.Name, foreground));
            }
        }
        catch (NetworkInformationException)
        {
            panel.Children.Add(Text("Network details are unavailable", foreground));
        }
        var quickSettings = ActionButton("Wi-Fi and Bluetooth", foreground, surface);
        quickSettings.Click += (_, _) => openQuickSettings();
        quickSettings.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(quickSettings);
        return panel;
    }

    internal static StackPanel Calendar(Brush foreground, Brush surface, Brush accent)
    {
        var culture = CultureInfo.CurrentCulture;
        var today = DateTime.Today;
        var month = new DateTime(today.Year, today.Month, 1);
        var panel = new StackPanel { Width = 246 };
        var title = Heading("", foreground);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(0, 0, 6, 0);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var next = ActionButton("\u203A", foreground, surface);
        var previous = ActionButton("\u2039", foreground, surface);
        System.Windows.Automation.AutomationProperties.SetName(previous, "Previous month");
        System.Windows.Automation.AutomationProperties.SetName(next, "Next month");
        previous.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(previous, Dock.Right);
        header.Children.Add(next);
        header.Children.Add(previous);
        header.Children.Add(title);
        panel.Children.Add(header);
        var dates = new UniformGrid { Columns = 7 };
        panel.Children.Add(dates);
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;

        void ShowMonth()
        {
            title.Text = month.ToString("MMMM yyyy", culture);
            dates.Children.Clear();
            for (int column = 0; column < 7; column++)
            {
                var day = (DayOfWeek)(((int)firstDay + column) % 7);
                dates.Children.Add(new TextBlock { Text = culture.DateTimeFormat.GetShortestDayName(day),
                    Foreground = foreground, Opacity = 0.62, Height = 22, TextAlignment = TextAlignment.Center });
            }
            int offset = (7 + (int)month.DayOfWeek - (int)firstDay) % 7;
            for (int index = 0; index < 42; index++)
            {
                var date = month.AddDays(index - offset);
                var text = new TextBlock { Text = date.Day.ToString(culture), TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = date == today ? Brushes.Black : foreground,
                    Opacity = date.Month == month.Month ? 1 : 0.4 };
                dates.Children.Add(new Border { Height = 27, Margin = new Thickness(2), CornerRadius = new(7),
                    Background = date == today ? accent : Brushes.Transparent, Child = text });
            }
        }

        previous.Click += (_, _) => { month = month.AddMonths(-1); ShowMonth(); };
        next.Click += (_, _) => { month = month.AddMonths(1); ShowMonth(); };
        ShowMonth();
        return panel;
    }

    private static Button ActionButton(string label, Brush foreground, Brush surface)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, surface);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.76));
        template.Triggers.Add(hover);
        return new Button { Content = label, Foreground = foreground, Template = template, MinHeight = 29 };
    }

    private static ControlTemplate VolumeTrack(Brush surface, Brush accent)
    {
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse($$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Slider">
              <Grid Background="Transparent" Height="24">
                <Border Height="6" CornerRadius="3" VerticalAlignment="Center" Background="{{surface}}" />
                <Track Name="PART_Track">
                  <Track.Thumb>
                    <Thumb Width="15">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Border Height="15" CornerRadius="8" Background="{{accent}}" />
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                </Track>
              </Grid>
            </ControlTemplate>
            """);
    }

    private static TextBlock Heading(string text, Brush foreground)
    {
        var heading = Text(text, foreground);
        heading.FontWeight = FontWeights.SemiBold;
        return heading;
    }

    private static TextBlock Text(string text, Brush foreground) => new()
    {
        Text = text, Foreground = foreground, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 3)
    };
}

internal sealed class AudioEndpoint : IDisposable
{
    private readonly EndpointVolume endpoint;

    private AudioEndpoint(EndpointVolume endpoint) => this.endpoint = endpoint;

    internal static AudioEndpoint? Open()
    {
        var classId = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        var interfaceId = typeof(EndpointVolume).GUID;
        DeviceEnumerator? enumerator = null;
        AudioDevice? device = null;
        try
        {
            enumerator = (DeviceEnumerator?)Activator.CreateInstance(Type.GetTypeFromCLSID(classId, true)!);
            if (enumerator == null || enumerator.DefaultEndpoint(0, 1, out device) != 0 ||
                device.Activate(in interfaceId, 23, 0, out var result) != 0)
                return null;
            return new AudioEndpoint((EndpointVolume)result);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (device != null)
                Marshal.ReleaseComObject(device);
            if (enumerator != null)
                Marshal.ReleaseComObject(enumerator);
        }
    }

    internal float Level
    {
        get => endpoint.GetScalar(out float value) == 0 ? value : 0;
        set => endpoint.SetScalar(value, 0);
    }

    internal bool Muted
    {
        get => endpoint.GetMute(out bool mute) == 0 && mute;
        set => endpoint.SetMute(value, 0);
    }

    public void Dispose() => Marshal.ReleaseComObject(endpoint);

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface DeviceEnumerator
    {
        [PreserveSig] int EnumEndpoints(int flow, int states, out nint result);
        [PreserveSig] int DefaultEndpoint(int flow, int role, out AudioDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface AudioDevice
    {
        [PreserveSig] int Activate(in Guid id, int context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface EndpointVolume
    {
        [PreserveSig] int Register(nint callback);
        [PreserveSig] int Unregister(nint callback);
        [PreserveSig] int ChannelCount(out uint channels);
        [PreserveSig] int SetMasterDb(float level, nint context);
        [PreserveSig] int SetScalar(float level, nint context);
        [PreserveSig] int GetMasterDb(out float level);
        [PreserveSig] int GetScalar(out float level);
        [PreserveSig] int SetChannelDb(uint channel, float level, nint context);
        [PreserveSig] int SetChannelScalar(uint channel, float level, nint context);
        [PreserveSig] int GetChannelDb(uint channel, out float level);
        [PreserveSig] int GetChannelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, nint context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
