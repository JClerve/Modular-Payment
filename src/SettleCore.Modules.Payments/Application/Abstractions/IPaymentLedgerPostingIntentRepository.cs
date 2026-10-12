using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public interface IPaymentLedgerPostingIntentRepository
{
    Task AddAsync(
        PaymentLedgerPostingIntent intent,
        CancellationToken cancellationToken = default);

    Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken = default);
    Task<bool> MarkPostedAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default);
    Task<bool> ScheduleRetryAsync(
        PaymentId paymentId,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);
}
