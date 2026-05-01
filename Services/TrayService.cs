using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace BalanceDock.Services;

public sealed class TrayService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly BalanceService _balanceService;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _startupMenuItem;

    public TrayService(
        MainWindow mainWindow,
        BalanceService balanceService,
        SettingsService settingsService,
        StartupService startupService)
    {
        _mainWindow = mainWindow;
        _balanceService = balanceService;
        _settingsService = settingsService;
        _startupService = startupService;

        _startupMenuItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            Checked = _settingsService.Current.StartWithWindows,
            CheckOnClick = false
        };
        _startupMenuItem.Click += (_, _) => ToggleStartup();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open BalanceDock", null, (_, _) => ShowWindow());
        menu.Items.Add("Reset Balance", null, (_, _) => _balanceService.Reset());
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "BalanceDock",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.MouseUp += OnTrayMouseUp;
    }

    public void ShowWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void OnTrayMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            if (_mainWindow.IsVisible && _mainWindow.IsActive)
            {
                _mainWindow.Hide();
            }
            else
            {
                ShowWindowNearTray();
            }
        }
    }

    private void ShowWindowNearTray()
    {
        var area = SystemParameters.WorkArea;
        _mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _mainWindow.Left = Math.Max(area.Left, area.Right - _mainWindow.Width - 16);
        _mainWindow.Top = Math.Max(area.Top, area.Bottom - _mainWindow.Height - 16);
        ShowWindow();
    }

    private void ToggleStartup()
    {
        var enabled = !_settingsService.Current.StartWithWindows;
        try
        {
            _startupService.SetEnabled(enabled);
            _settingsService.Current.StartWithWindows = enabled;
            _settingsService.Save();
            _startupMenuItem.Checked = enabled;
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show(
                $"Could not update Windows startup setting: {ex.Message}",
                "BalanceDock",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _mainWindow.AllowClose = true;
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
