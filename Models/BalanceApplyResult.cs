namespace BalanceDock.Models;

public enum BalanceApplyMode
{
    None,
    Endpoint,
    SessionFallback
}

public sealed record BalanceApplyResult(
    BalanceApplyMode Mode,
    int SessionsUpdated,
    string Message)
{
    public static BalanceApplyResult None(string message) => new(BalanceApplyMode.None, 0, message);
}
