namespace BalanceDock.Models;

public sealed class AppSettings
{
    public int Balance { get; set; }
    public bool StartWithWindows { get; set; }
    public int HotkeyStep { get; set; } = 5;
    public string ShiftLeftHotkey { get; set; } = "Ctrl+Alt+Left";
    public string ShiftRightHotkey { get; set; } = "Ctrl+Alt+Right";
    public string ResetHotkey { get; set; } = "Ctrl+Alt+Down";
    public bool ResetOnExit { get; set; }
}
