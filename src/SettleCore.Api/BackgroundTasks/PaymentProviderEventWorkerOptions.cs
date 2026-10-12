namespace SettleCore.Api.BackgroundTasks;

public sealed class PaymentProviderEventWorkerOptions
{
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 100;
    public int PollIntervalMilliseconds { get; set; } = 5_000;
    public int RetryDelaySeconds { get; set; } = 60;
}
