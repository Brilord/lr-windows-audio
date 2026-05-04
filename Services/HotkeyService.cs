using System.Runtime.InteropServices;
using System.Windows.Interop;
using BalanceDock.Models;

namespace BalanceDock.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const int LeftHotkeyId = 1001;
    private const int RightHotkeyId = 1002;
    private const int ResetHotkeyId = 1003;

    private static readonly Dictionary<string, uint> SupportedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Left"] = 0x25,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["A"] = 0x41,
        ["D"] = 0x44,
        ["S"] = 0x53,
        ["J"] = 0x4A,
        ["K"] = 0x4B,
        ["L"] = 0x4C,
    };

    private IntPtr _windowHandle;
    private bool _messageHookAttached;

    public event EventHandler? ShiftLeftRequested;
    public event EventHandler? ShiftRightRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler<string>? ErrorOccurred;

    public static IReadOnlyList<string> SupportedHotkeys { get; } =
    [
        "Ctrl+Alt+Left",
        "Ctrl+Alt+Right",
        "Ctrl+Alt+Down",
        "Ctrl+Alt+A",
        "Ctrl+Alt+D",
        "Ctrl+Alt+S",
        "Ctrl+Shift+Left",
        "Ctrl+Shift+Right",
        "Ctrl+Shift+Down",
        "Ctrl+Shift+J",
        "Ctrl+Shift+K",
        "Ctrl+Shift+L",
    ];

    public static bool IsSupportedHotkey(string? hotkey) =>
        !string.IsNullOrWhiteSpace(hotkey) && SupportedHotkeys.Contains(hotkey, StringComparer.OrdinalIgnoreCase);

    public void Register(IntPtr windowHandle, AppSettings settings)
    {
        _windowHandle = windowHandle;
        if (!_messageHookAttached)
        {
            ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
            _messageHookAttached = true;
        }

        Reconfigure(settings);
    }

    public void Reconfigure(AppSettings settings)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterAll();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leftOk = RegisterConfiguredHotkey(LeftHotkeyId, settings.ShiftLeftHotkey, used);
        var rightOk = RegisterConfiguredHotkey(RightHotkeyId, settings.ShiftRightHotkey, used);
        var resetOk = RegisterConfiguredHotkey(ResetHotkeyId, settings.ResetHotkey, used);

        if (!leftOk || !rightOk || !resetOk)
        {
            ErrorOccurred?.Invoke(this, "One or more global hotkeys could not be registered. Another app may already use them, or a duplicate hotkey was selected.");
        }
    }

    private bool RegisterConfiguredHotkey(int id, string hotkey, HashSet<string> used)
    {
        if (!TryParseHotkey(hotkey, out var modifiers, out var virtualKey) || !used.Add(hotkey))
        {
            return false;
        }

        // RegisterHotKey installs process-wide shortcuts handled by the message loop for this window.
        return RegisterHotKey(_windowHandle, id, modifiers | ModNoRepeat, virtualKey);
    }

    private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ModControl,
                "ALT" => ModAlt,
                "SHIFT" => ModShift,
                _ => 0
            };
        }

        return modifiers != 0 && SupportedKeys.TryGetValue(parts[^1], out virtualKey);
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
        if (_messageHookAttached)
        {
            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            _messageHookAttached = false;
        }

        UnregisterAll();
    }

    private void UnregisterAll()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(_windowHandle, LeftHotkeyId);
        UnregisterHotKey(_windowHandle, RightHotkeyId);
        UnregisterHotKey(_windowHandle, ResetHotkeyId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
