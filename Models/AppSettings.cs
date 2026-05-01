namespace BalanceDock.Models;

public sealed class AppSettings
{
    public int Balance { get; set; }
    public bool StartWithWindows { get; set; }
    public int HotkeyStep { get; set; } = 5;
}
