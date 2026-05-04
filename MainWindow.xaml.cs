using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using BalanceDock.Services;

namespace BalanceDock;

public partial class MainWindow : Window
{
    private readonly AudioDeviceService _audioDeviceService;
    private readonly BalanceService _balanceService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private readonly LogService _logService;
    private bool _isUpdatingUi;

    public bool AllowClose { get; set; }

    public MainWindow(
        AudioDeviceService audioDeviceService,
        BalanceService balanceService,
        SettingsService settingsService,
        HotkeyService hotkeyService,
        LogService logService)
    {
        _audioDeviceService = audioDeviceService;
        _balanceService = balanceService;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _logService = logService;

        InitializeComponent();
        SetWindowIcon();
        InitializeSettingsControls();
        RefreshDeviceName();
        RefreshDiagnostics();
        SetSliderValue(_settingsService.Current.Balance);
        UpdatePercentages(_settingsService.Current.Balance);
        ModeText.Text = "Ready";
        StatusText.Text = CurrentHotkeyText();
        LogPathText.Text = $"Log: {_logService.LogPath}";

        _balanceService.BalanceChanged += (_, balance) => Dispatcher.Invoke(() =>
        {
            SetSliderValue(balance);
            UpdatePercentages(balance);
            RefreshDiagnostics();
        });
        _balanceService.ErrorOccurred += (_, message) => Dispatcher.Invoke(() =>
        {
            ModeText.Text = "No controllable audio sessions found";
            StatusText.Text = message;
            RefreshDiagnostics();
        });
        _balanceService.StatusChanged += (_, message) => Dispatcher.Invoke(() =>
        {
            ModeText.Text = message;
            StatusText.Text = CurrentHotkeyText();
            RefreshDiagnostics();
        });

        _hotkeyService.ShiftLeftRequested += (_, _) => _balanceService.Shift(-_settingsService.Current.HotkeyStep);
        _hotkeyService.ShiftRightRequested += (_, _) => _balanceService.Shift(_settingsService.Current.HotkeyStep);
        _hotkeyService.ResetRequested += (_, _) => _balanceService.Reset();
        _hotkeyService.ErrorOccurred += (_, message) => Dispatcher.Invoke(() => StatusText.Text = message);
    }

    public void RefreshDeviceName()
    {
        DeviceNameText.Text = _audioDeviceService.GetDefaultOutputDeviceName();
    }

    private void RefreshDiagnostics()
    {
        var diagnostics = _balanceService.GetDiagnostics();
        DeviceInfoText.Text =
            $"Endpoint channels: {diagnostics.EndpointChannelCount}\n" +
            $"Endpoint balance supported: {(diagnostics.EndpointBalanceSupported ? "Yes" : "No")}\n" +
            $"Active sessions: {diagnostics.ActiveSessionCount}\n" +
            $"Controllable sessions: {diagnostics.ControllableSessionCount}\n" +
            $"Fallback available: {(diagnostics.SessionFallbackAvailable ? "Yes" : "No")}";
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BalanceDock.ico");
        if (File.Exists(iconPath))
        {
            Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }
    }

    private void InitializeSettingsControls()
    {
        _isUpdatingUi = true;

        LeftHotkeyComboBox.ItemsSource = HotkeyService.SupportedHotkeys;
        RightHotkeyComboBox.ItemsSource = HotkeyService.SupportedHotkeys;
        ResetHotkeyComboBox.ItemsSource = HotkeyService.SupportedHotkeys;

        LeftHotkeyComboBox.SelectedItem = _settingsService.Current.ShiftLeftHotkey;
        RightHotkeyComboBox.SelectedItem = _settingsService.Current.ShiftRightHotkey;
        ResetHotkeyComboBox.SelectedItem = _settingsService.Current.ResetHotkey;
        StepComboBox.SelectedIndex = _settingsService.Current.HotkeyStep switch
        {
            1 => 0,
            2 => 1,
            10 => 3,
            25 => 4,
            _ => 2
        };
        ResetOnExitCheckBox.IsChecked = _settingsService.Current.ResetOnExit;

        _isUpdatingUi = false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Register(handle, _settingsService.Current);
    }

    private void OnBalanceSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || !IsLoaded)
        {
            return;
        }

        var balance = (int)Math.Round(e.NewValue);
        UpdatePercentages(balance);
        _balanceService.SetBalance(balance);
    }

    private void OnResetClicked(object sender, RoutedEventArgs e) => _balanceService.Reset();

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        AllowClose = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnRetryClicked(object sender, RoutedEventArgs e)
    {
        RefreshDeviceName();
        RefreshDiagnostics();
        _balanceService.ApplySavedBalance();
    }

    private void OnHotkeySettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || !IsLoaded)
        {
            return;
        }

        SaveHotkeySettings();
    }

    private void OnResetOnExitChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || !IsLoaded)
        {
            return;
        }

        _settingsService.Current.ResetOnExit = ResetOnExitCheckBox.IsChecked == true;
        _settingsService.Save();
        _logService.Info($"ResetOnExit changed to {_settingsService.Current.ResetOnExit}.");
    }

    private void SaveHotkeySettings()
    {
        _settingsService.Current.ShiftLeftHotkey = LeftHotkeyComboBox.SelectedItem as string ?? "Ctrl+Alt+Left";
        _settingsService.Current.ShiftRightHotkey = RightHotkeyComboBox.SelectedItem as string ?? "Ctrl+Alt+Right";
        _settingsService.Current.ResetHotkey = ResetHotkeyComboBox.SelectedItem as string ?? "Ctrl+Alt+Down";

        if (StepComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out var step))
        {
            _settingsService.Current.HotkeyStep = Math.Clamp(step, 1, 25);
        }

        _settingsService.Save();
        _hotkeyService.Reconfigure(_settingsService.Current);
        StatusText.Text = CurrentHotkeyText();
        _logService.Info("Hotkey settings updated.");
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void SetSliderValue(int balance)
    {
        _isUpdatingUi = true;
        BalanceSlider.Value = balance;
        _isUpdatingUi = false;
    }

    private void UpdatePercentages(int balance)
    {
        var (left, right) = _balanceService.GetDisplayPercentages(balance);
        LeftPercentText.Text = $"L {left}%";
        RightPercentText.Text = $"R {right}%";
    }

    private string CurrentHotkeyText() =>
        $"Hotkeys: {_settingsService.Current.ShiftLeftHotkey}, {_settingsService.Current.ShiftRightHotkey}, {_settingsService.Current.ResetHotkey}. Step: {_settingsService.Current.HotkeyStep}%.";
}
