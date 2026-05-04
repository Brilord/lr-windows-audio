using System.Windows;
using System.Windows.Interop;
using BalanceDock.Services;

namespace BalanceDock;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showMainWindowEvent;
    private RegisteredWaitHandle? _showMainWindowRegistration;
    private SettingsService? _settingsService;
    private LogService? _logService;
    private StartupService? _startupService;
    private AudioDeviceService? _audioDeviceService;
    private BalanceService? _balanceService;
    private HotkeyService? _hotkeyService;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "Local\\BalanceDock.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _showMainWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\BalanceDock.ShowMainWindow");

        _settingsService = new SettingsService();
        _settingsService.Load();
        _logService = new LogService();
        _logService.Info("BalanceDock starting.");

        _startupService = new StartupService();
        _settingsService.Current.StartWithWindows = _startupService.IsEnabled();
        _settingsService.Save();

        _audioDeviceService = new AudioDeviceService();
        _balanceService = new BalanceService(_audioDeviceService, _settingsService, _logService);
        _hotkeyService = new HotkeyService();

        _mainWindow = new MainWindow(_audioDeviceService, _balanceService, _settingsService, _hotkeyService, _logService);
        _ = new WindowInteropHelper(_mainWindow).EnsureHandle();
        _trayService = new TrayService(_mainWindow, _balanceService, _settingsService, _startupService);
        _showMainWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showMainWindowEvent,
            (_, _) => Dispatcher.Invoke(() => _trayService.ShowWindow()),
            null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);

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
        if (_settingsService?.Current.ResetOnExit == true)
        {
            _balanceService?.Reset();
            _logService?.Info("Reset balance on exit.");
        }

        _logService?.Info("BalanceDock exiting.");
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _balanceService?.Dispose();
        _audioDeviceService?.Dispose();
        _showMainWindowRegistration?.Unregister(null);
        _showMainWindowEvent?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting("Local\\BalanceDock.ShowMainWindow");
            showEvent.Set();
        }
        catch
        {
            // If the first instance is still starting or shutting down, there may be no window to show.
        }
    }
}
