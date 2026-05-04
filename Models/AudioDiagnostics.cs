namespace BalanceDock.Models;

public sealed record AudioDiagnostics(
    string DeviceName,
    int EndpointChannelCount,
    bool EndpointBalanceSupported,
    int ActiveSessionCount,
    int ControllableSessionCount,
    bool SessionFallbackAvailable)
{
    public static AudioDiagnostics NoDevice { get; } = new(
        "No active output device",
        0,
        false,
        0,
        0,
        false);
}
