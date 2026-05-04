using System.Reflection;
using System.Runtime.InteropServices;
using BalanceDock.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace BalanceDock.Services;

public sealed class BalanceService : IDisposable
{
    private static readonly FieldInfo? AudioSessionControlField =
        typeof(AudioSessionControl).GetField("audioSessionControlInterface", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly AudioDeviceService _audioDeviceService;
    private readonly SettingsService _settingsService;
    private readonly LogService _logService;
    private readonly System.Threading.Timer _sessionFallbackTimer;
    private readonly object _syncRoot = new();
    private MMDevice? _fallbackDevice;
    private AudioSessionManager.SessionCreatedDelegate? _sessionCreatedHandler;
    private bool _sessionFallbackActive;
    private bool _disposed;

    public BalanceService(AudioDeviceService audioDeviceService, SettingsService settingsService, LogService logService)
    {
        _audioDeviceService = audioDeviceService;
        _settingsService = settingsService;
        _logService = logService;
        _sessionFallbackTimer = new System.Threading.Timer(_ => ReapplySessionFallback(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<int>? BalanceChanged;

    public int CurrentBalance => _settingsService.Current.Balance;

    public (int LeftPercent, int RightPercent) GetDisplayPercentages(int balance)
    {
        balance = Math.Clamp(balance, -100, 100);
        return ((100 - balance) / 2, (100 + balance) / 2);
    }

    public AudioDiagnostics GetDiagnostics()
    {
        try
        {
            using var device = _audioDeviceService.GetDefaultOutputDevice();
            if (device is null)
            {
                return AudioDiagnostics.NoDevice;
            }

            var endpointChannels = device.AudioEndpointVolume.Channels.Count;
            var sessions = device.AudioSessionManager.Sessions;
            var activeSessionCount = 0;
            var controllableSessionCount = 0;

            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateActive)
                {
                    activeSessionCount++;
                }

                if (CanControlSessionChannels(session))
                {
                    controllableSessionCount++;
                }
            }

            return new AudioDiagnostics(
                device.FriendlyName,
                endpointChannels,
                endpointChannels >= 2,
                activeSessionCount,
                controllableSessionCount,
                controllableSessionCount > 0);
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to read audio diagnostics.", ex);
            return AudioDiagnostics.NoDevice;
        }
    }

    public bool ApplySavedBalance() => SetBalance(_settingsService.Current.Balance, persist: false);

    public bool Reset() => SetBalance(0);

    public bool Shift(int delta) => SetBalance(_settingsService.Current.Balance + delta);

    public bool SetBalance(int balance, bool persist = true)
    {
        balance = Math.Clamp(balance, -100, 100);

        lock (_syncRoot)
        {
            return SetBalanceCore(balance, persist, raiseEvents: true);
        }
    }

    private bool SetBalanceCore(int balance, bool persist, bool raiseEvents)
    {
        try
        {
            using var device = _audioDeviceService.GetDefaultOutputDevice();
            if (device is null)
            {
                ReportError("No active output device was found.");
                return false;
            }

            var endpointVolume = device.AudioEndpointVolume;
            var applied = false;

            if (endpointVolume.Channels.Count >= 2)
            {
                ApplyEndpointBalance(endpointVolume, balance);
                applied = true;
                StopSessionFallback();
                if (raiseEvents)
                {
                    StatusChanged?.Invoke(this, "Device balance active");
                }
            }
            else
            {
                var sessionsUpdated = ApplySessionFallbackBalance(device, balance);
                if (sessionsUpdated > 0 || balance == 0)
                {
                    applied = true;
                    if (balance != 0)
                    {
                        _sessionFallbackActive = true;
                        StartSessionFallbackMonitor(balance);
                    }
                    else
                    {
                        StopSessionFallback();
                    }

                    if (raiseEvents)
                    {
                        StatusChanged?.Invoke(this, $"Per-app fallback active. {sessionsUpdated} session(s) adjusted.");
                    }
                }
            }

            if (!applied)
            {
                ReportError("No controllable audio sessions found. Start playback and press Retry.");
                return false;
            }

            _settingsService.Current.Balance = balance;
            if (persist)
            {
                _settingsService.Save();
            }

            BalanceChanged?.Invoke(this, balance);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Balance apply failed.", ex);
            ReportError($"Windows audio API access failed: {ex.Message}");
            return false;
        }
    }

    private static void ApplyEndpointBalance(AudioEndpointVolume endpointVolume, int balance)
    {
        var (leftScalar, rightScalar) = GetChannelScalars(balance);

        // Windows endpoint balance is applied by setting per-channel scalar volumes.
        // At center both channels stay at 100%; moving toward one side attenuates the opposite channel.
        endpointVolume.Channels[0].VolumeLevelScalar = leftScalar;
        endpointVolume.Channels[1].VolumeLevelScalar = rightScalar;
    }

    private static int ApplySessionFallbackBalance(MMDevice device, int balance)
    {
        var sessions = device.AudioSessionManager.Sessions;
        var (leftScalar, rightScalar) = GetChannelScalars(balance);
        var updated = 0;

        for (var i = 0; i < sessions.Count; i++)
        {
            if (TryApplySessionChannelBalance(sessions[i], leftScalar, rightScalar))
            {
                updated++;
            }
        }

        return updated;
    }

    private static bool CanControlSessionChannels(AudioSessionControl session)
    {
        if (AudioSessionControlField?.GetValue(session) is not object rawSessionControl)
        {
            return false;
        }

        return CanControlSessionChannels(rawSessionControl);
    }

    private static bool CanControlSessionChannels(object rawSessionControl)
    {
        var unknown = IntPtr.Zero;
        var channelVolumePointer = IntPtr.Zero;

        try
        {
            unknown = Marshal.GetIUnknownForObject(rawSessionControl);
            var iid = typeof(IChannelAudioVolume).GUID;
            if (Marshal.QueryInterface(unknown, ref iid, out channelVolumePointer) != 0 || channelVolumePointer == IntPtr.Zero)
            {
                return false;
            }

            var channelVolume = (IChannelAudioVolume)Marshal.GetObjectForIUnknown(channelVolumePointer);
            return channelVolume.GetChannelCount(out var channelCount) == 0 && channelCount >= 2;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (channelVolumePointer != IntPtr.Zero)
            {
                Marshal.Release(channelVolumePointer);
            }

            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    private static bool TryApplySessionChannelBalance(AudioSessionControl session, float leftScalar, float rightScalar)
    {
        if (AudioSessionControlField?.GetValue(session) is not object rawSessionControl)
        {
            return false;
        }

        return TryApplySessionChannelBalance(rawSessionControl, leftScalar, rightScalar);
    }

    private static bool TryApplySessionChannelBalance(object rawSessionControl, float leftScalar, float rightScalar)
    {
        var unknown = IntPtr.Zero;
        var channelVolumePointer = IntPtr.Zero;

        try
        {
            unknown = Marshal.GetIUnknownForObject(rawSessionControl);
            var iid = typeof(IChannelAudioVolume).GUID;
            if (Marshal.QueryInterface(unknown, ref iid, out channelVolumePointer) != 0 || channelVolumePointer == IntPtr.Zero)
            {
                return false;
            }

            var channelVolume = (IChannelAudioVolume)Marshal.GetObjectForIUnknown(channelVolumePointer);
            if (channelVolume.GetChannelCount(out var channelCount) != 0 || channelCount < 2)
            {
                return false;
            }

            var context = Guid.Empty;
            var volumes = new float[channelCount];
            volumes[0] = leftScalar;
            volumes[1] = rightScalar;
            for (var i = 2; i < volumes.Length; i++)
            {
                volumes[i] = 1.0f;
            }

            return channelVolume.SetAllVolumes(channelCount, volumes, ref context) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (channelVolumePointer != IntPtr.Zero)
            {
                Marshal.Release(channelVolumePointer);
            }

            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    private void StartSessionFallbackMonitor(int balance)
    {
        if (_fallbackDevice is not null)
        {
            _sessionFallbackTimer.Change(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8));
            return;
        }

        _fallbackDevice = _audioDeviceService.GetDefaultOutputDevice();
        if (_fallbackDevice is null)
        {
            return;
        }

        _sessionCreatedHandler = (_, newSession) =>
        {
            var (leftScalar, rightScalar) = GetChannelScalars(_settingsService.Current.Balance);
            if (TryApplySessionChannelBalance(newSession, leftScalar, rightScalar))
            {
                _logService.Info("Applied fallback balance to newly-created audio session.");
            }
        };

        _fallbackDevice.AudioSessionManager.OnSessionCreated += _sessionCreatedHandler;
        _sessionFallbackTimer.Change(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8));
        _logService.Info($"Session fallback monitor started at balance {balance}.");
    }

    private void StopSessionFallback()
    {
        _sessionFallbackActive = false;
        _sessionFallbackTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (_fallbackDevice is not null && _sessionCreatedHandler is not null)
        {
            try
            {
                _fallbackDevice.AudioSessionManager.OnSessionCreated -= _sessionCreatedHandler;
            }
            catch (Exception ex)
            {
                _logService.Error("Failed to unsubscribe from session-created notifications.", ex);
            }
        }

        _sessionCreatedHandler = null;
        _fallbackDevice?.Dispose();
        _fallbackDevice = null;
    }

    private void ReapplySessionFallback()
    {
        lock (_syncRoot)
        {
            if (!_sessionFallbackActive || _disposed)
            {
                return;
            }

            try
            {
                using var device = _audioDeviceService.GetDefaultOutputDevice();
                if (device is not null)
                {
                    var updated = ApplySessionFallbackBalance(device, _settingsService.Current.Balance);
                    _logService.Info($"Fallback safety reapply adjusted {updated} session(s).");
                }
            }
            catch (Exception ex)
            {
                _logService.Error("Fallback safety reapply failed.", ex);
            }
        }
    }

    private static (float Left, float Right) GetChannelScalars(int balance)
    {
        var leftScalar = balance <= 0 ? 1.0f : 1.0f - balance / 100.0f;
        var rightScalar = balance >= 0 ? 1.0f : 1.0f + balance / 100.0f;
        return (Math.Clamp(leftScalar, 0, 1), Math.Clamp(rightScalar, 0, 1));
    }

    private void ReportError(string message) => ErrorOccurred?.Invoke(this, message);

    public void Dispose()
    {
        _disposed = true;
        StopSessionFallback();
        _sessionFallbackTimer.Dispose();
    }

    [ComImport]
    [Guid("1C158861-B533-4B30-B1CF-E853E51C59B8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IChannelAudioVolume
    {
        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetChannelVolume(uint channelIndex, float volume, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolume(uint channelIndex, out float volume);

        [PreserveSig]
        int SetAllVolumes(uint channelCount, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] volumes, ref Guid eventContext);

        [PreserveSig]
        int GetAllVolumes(uint channelCount, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] volumes);
    }
}
