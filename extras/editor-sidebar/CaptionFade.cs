using System.Runtime.InteropServices;

namespace EditorSidebar;

internal sealed class CaptionFade : IDisposable
{
    private delegate nint MouseCallback(int code, nint message, nint data);
    private delegate void EventCallback(nint hook, uint type, nint window, int objectId, int childId, uint thread, uint time);

    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint SetWindowsHookEx(int type, MouseCallback callback, nint module, uint thread);
    [DllImport("user32")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32")] private static extern nint SetWinEventHook(uint minimum, uint maximum, nint module,
        EventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32")] private static extern bool UnhookWinEvent(nint hook);

    private readonly MouseCallback mouseCallback;
    private readonly EventCallback eventCallback;
    private readonly nint mouseHook, eventHook;
    private readonly Func<nint, bool> canBegin;
    private readonly Action<nint> begin;
    private readonly Action<nint, int> finish;
    private nint pressedWindow;
    private int pressedAction;

    internal CaptionFade(Func<nint, bool> canBegin, Action<nint> begin, Action<nint, int> finish, Action<nint> minimizing)
    {
        this.canBegin = canBegin;
        this.begin = begin;
        this.finish = finish;
        mouseCallback = MouseInput;
        eventCallback = (_, _, window, _, _, _, _) => minimizing(window);
        mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
        eventHook = SetWinEventHook(0x0016, 0x0016, 0, eventCallback, 0, 0, 2);
    }

    private nint MouseInput(int code, nint message, nint data)
    {
        if (code == 0 && message == 0x0201)
        {
            nint active = Native.GetForegroundWindow();
            if (canBegin(active))
            {
                int action = Native.CaptionAction(active, Marshal.PtrToStructure<Native.Point>(data));
                if (action != 0)
                {
                    pressedWindow = active;
                    pressedAction = action;
                    begin(active);
                    return 1;
                }
            }
        }
        else if (code == 0 && message == 0x0202 && pressedWindow != 0)
        {
            nint window = pressedWindow;
            pressedWindow = 0;
            int action = Native.GetForegroundWindow() == window
                ? Native.CaptionAction(window, Marshal.PtrToStructure<Native.Point>(data)) : 0;
            finish(window, action == pressedAction ? action : 0);
            return 1;
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }

    public void Dispose()
    {
        if (mouseHook != 0)
            UnhookWindowsHookEx(mouseHook);
        if (eventHook != 0)
            UnhookWinEvent(eventHook);
    }
}
