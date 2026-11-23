namespace EditorSidebar;

internal sealed class MaximizedCorners
{
    internal const double Radius = 12;
    private readonly Dictionary<nint, (Native.Rect Rect, int Diameter)> applied = new();
    private readonly HashSet<nint> unsupported = new();

    internal void Apply(nint window, Native.Rect frame)
    {
        if (unsupported.Contains(window) || !Native.IsZoomed(window) || !Native.GetWindowRect(window, out var full))
            return;

        uint dpi = Native.GetDpiForWindow(window);
        int diameter = (int)Math.Round(2 * Radius * (dpi == 0 ? 96 : dpi) / 96.0);
        var shape = (new Native.Rect(frame.Left - full.Left, frame.Top - full.Top,
            frame.Right - full.Left, frame.Bottom - full.Top), diameter);
        if (applied.TryGetValue(window, out var original) && original == shape)
            return;

        Restore(window);
        nint probe = Create(shape);
        if (probe == 0)
            return;
        bool appOwnsRegion = Native.GetWindowRgn(window, probe) != 0;
        Native.DeleteObject(probe);
        if (appOwnsRegion)
        {
            unsupported.Add(window);
            return;
        }

        nint region = Create(shape);
        if (region == 0)
            return;
        if (Native.SetWindowRgn(window, region, true) == 0)
        {
            Native.DeleteObject(region);
            unsupported.Add(window);
            return;
        }
        applied[window] = shape;
    }

    internal void Refresh()
    {
        unsupported.RemoveWhere(window => !Native.IsWindow(window));
        foreach (nint window in applied.Keys.ToArray())
        {
            if (!Native.IsZoomed(window) || !Native.TryGetAppFrame(window, out var frame))
                Restore(window);
            else
                Apply(window, frame);
        }
    }

    internal void Clear()
    {
        foreach (nint window in applied.Keys.ToArray())
            Restore(window);
    }

    private void Restore(nint window)
    {
        if (!applied.Remove(window, out var shape) || !Native.IsWindow(window))
            return;
        nint original = Create(shape);
        nint actual = Create(shape);
        try
        {
            if (original != 0 && actual != 0 && Native.GetWindowRgn(window, actual) != 0 &&
                Native.EqualRgn(original, actual))
                Native.SetWindowRgn(window, 0, true);
        }
        finally
        {
            if (original != 0)
                Native.DeleteObject(original);
            if (actual != 0)
                Native.DeleteObject(actual);
        }
    }

    private static nint Create((Native.Rect Rect, int Diameter) shape) => Native.CreateRoundRectRgn(
        shape.Rect.Left, shape.Rect.Top, shape.Rect.Right, shape.Rect.Bottom, shape.Diameter, shape.Diameter);
}
