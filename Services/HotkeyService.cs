using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace BalanceDock.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkLeft = 0x25;
    private const uint VkRight = 0x27;
    private const uint VkDown = 0x28;
    private const int LeftHotkeyId = 1001;
    private const int RightHotkeyId = 1002;
    private const int ResetHotkeyId = 1003;

    private IntPtr _windowHandle;
    private bool _registered;

    public event EventHandler? ShiftLeftRequested;
    public event EventHandler? ShiftRightRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler<string>? ErrorOccurred;

    public void Register(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

        // RegisterHotKey installs process-wide shortcuts handled by the message loop for this window.
        var modifiers = ModControl | ModAlt | ModNoRepeat;
        var leftOk = RegisterHotKey(_windowHandle, LeftHotkeyId, modifiers, VkLeft);
        var rightOk = RegisterHotKey(_windowHandle, RightHotkeyId, modifiers, VkRight);
        var resetOk = RegisterHotKey(_windowHandle, ResetHotkeyId, modifiers, VkDown);
        _registered = leftOk && rightOk && resetOk;

        if (!_registered)
        {
            ErrorOccurred?.Invoke(this, "One or more global hotkeys could not be registered. Another app may already use them.");
        }
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey)
        {
            return;
        }

        handled = true;
        switch (msg.wParam.ToInt32())
        {
            case LeftHotkeyId:
                ShiftLeftRequested?.Invoke(this, EventArgs.Empty);
                break;
            case RightHotkeyId:
                ShiftRightRequested?.Invoke(this, EventArgs.Empty);
                break;
            case ResetHotkeyId:
                ResetRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Dispose()
    {
        ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;

        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, LeftHotkeyId);
            UnregisterHotKey(_windowHandle, RightHotkeyId);
            UnregisterHotKey(_windowHandle, ResetHotkeyId);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
