using System.Windows;
using System.Windows.Interop;
using BalanceDock.Services;

namespace BalanceDock;

public partial class App : System.Windows.Application
{
    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private AudioDeviceService? _audioDeviceService;
    private BalanceService? _balanceService;
    private HotkeyService? _hotkeyService;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _settingsService = new SettingsService();
        _settingsService.Load();

        _startupService = new StartupService();
        _settingsService.Current.StartWithWindows = _startupService.IsEnabled();
        _settingsService.Save();

        _audioDeviceService = new AudioDeviceService();
        _balanceService = new BalanceService(_audioDeviceService, _settingsService);
        _hotkeyService = new HotkeyService();

        _mainWindow = new MainWindow(_audioDeviceService, _balanceService, _settingsService, _hotkeyService);
        _ = new WindowInteropHelper(_mainWindow).EnsureHandle();
        _trayService = new TrayService(_mainWindow, _balanceService, _settingsService, _startupService);

        _audioDeviceService.DefaultDeviceChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _mainWindow.RefreshDeviceName();
                _balanceService.ApplySavedBalance();
            });
        };

        _balanceService.ApplySavedBalance();
        if (!e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))
        {
            _trayService.ShowWindow();
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _balanceService?.Dispose();
        _audioDeviceService?.Dispose();
    }
}
