using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using BalanceDock.Services;

namespace BalanceDock;

public partial class MainWindow : Window
{
    private readonly AudioDeviceService _audioDeviceService;
    private readonly BalanceService _balanceService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private bool _isUpdatingUi;

    public bool AllowClose { get; set; }

    public MainWindow(
        AudioDeviceService audioDeviceService,
        BalanceService balanceService,
        SettingsService settingsService,
        HotkeyService hotkeyService)
    {
        _audioDeviceService = audioDeviceService;
        _balanceService = balanceService;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;

        InitializeComponent();
        RefreshDeviceName();
        SetSliderValue(_settingsService.Current.Balance);
        UpdatePercentages(_settingsService.Current.Balance);
        StatusText.Text = "Hotkeys: Ctrl+Alt+Left, Ctrl+Alt+Right, Ctrl+Alt+Down.";

        _balanceService.BalanceChanged += (_, balance) => Dispatcher.Invoke(() =>
        {
            SetSliderValue(balance);
            UpdatePercentages(balance);
            StatusText.Text = "Balance applied.";
        });
        _balanceService.ErrorOccurred += (_, message) => Dispatcher.Invoke(() => StatusText.Text = message);
        _balanceService.StatusChanged += (_, message) => Dispatcher.Invoke(() => StatusText.Text = message);

        _hotkeyService.ShiftLeftRequested += (_, _) => _balanceService.Shift(-_settingsService.Current.HotkeyStep);
        _hotkeyService.ShiftRightRequested += (_, _) => _balanceService.Shift(_settingsService.Current.HotkeyStep);
        _hotkeyService.ResetRequested += (_, _) => _balanceService.Reset();
        _hotkeyService.ErrorOccurred += (_, message) => Dispatcher.Invoke(() => StatusText.Text = message);
    }

    public void RefreshDeviceName()
    {
        DeviceNameText.Text = _audioDeviceService.GetDefaultOutputDeviceName();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Register(handle);
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

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
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
}
