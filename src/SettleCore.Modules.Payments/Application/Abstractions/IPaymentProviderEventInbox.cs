namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderEventInbox
{
    Task<IReadOnlyList<ProviderPaymentSucceededEvent>> GetPendingAsync(string provider, int batchSize,
        CancellationToken cancellationToken = default);
    Task<bool> ScheduleRetryAsync(string provider, string eventId, DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);
    Task<bool> ReceiveAsync(ProviderPaymentSucceededEvent evidence, CancellationToken cancellationToken = default);
}
