using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace BalanceDock.Services;

public sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DeviceNotificationClient _notificationClient;
    private bool _disposed;

    public AudioDeviceService()
    {
        _notificationClient = new DeviceNotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    public event EventHandler? DefaultDeviceChanged;

    public MMDevice? GetDefaultOutputDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public string GetDefaultOutputDeviceName()
    {
        using var device = GetDefaultOutputDevice();
        return device?.FriendlyName ?? "No active output device";
    }

    private void NotifyDefaultDeviceChanged() => DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        _enumerator.Dispose();
    }

    private sealed class DeviceNotificationClient(AudioDeviceService owner) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                owner.NotifyDefaultDeviceChanged();
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner.NotifyDefaultDeviceChanged();
        public void OnDeviceAdded(string pwstrDeviceId) => owner.NotifyDefaultDeviceChanged();
        public void OnDeviceRemoved(string deviceId) => owner.NotifyDefaultDeviceChanged();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) => owner.NotifyDefaultDeviceChanged();
    }
}
