using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentLedgerPostingIntentRepository(
    PaymentsDbContext dbContext, TimeProvider? timeProvider = null)
    : IPaymentLedgerPostingIntentRepository
{
    public async Task AddAsync(
        PaymentLedgerPostingIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        await dbContext.PaymentLedgerPostingIntents.AddAsync(
            intent,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PaymentLedgerPostingIntents.AsNoTracking().SingleOrDefaultAsync(
            intent => intent.PaymentId == paymentId,
            cancellationToken);
    }
    public async Task<bool> MarkPostedAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken = default)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        // One PostgreSQL statement commits the transition and event together.
        // Concurrent/replayed acknowledgments cannot append another event.
        var eventId = Guid.NewGuid();
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH acknowledged AS (
                UPDATE payment_ledger_posting_intents
                SET status = 'Posted', posted_at = {now}
                WHERE payment_id = {paymentId.Value} AND status = 'Pending'
                RETURNING payment_id, transaction_id
            )
            INSERT INTO payment_ledger_posting_events
                (id, payment_id, transaction_id, kind, occurred_at, next_attempt_at)
            SELECT {eventId}, payment_id, transaction_id, 'PostingAcknowledged', {now}, NULL
            FROM acknowledged
            """, cancellationToken);
        return affected == 1 || await dbContext.PaymentLedgerPostingIntents
            .AnyAsync(intent => intent.PaymentId == paymentId &&
                intent.Status == PaymentLedgerPostingIntentStatus.Posted, cancellationToken);
    }
    public async Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return await dbContext.PaymentLedgerPostingIntents.AsNoTracking()
            .Where(intent => intent.Status == PaymentLedgerPostingIntentStatus.Pending &&
                (intent.NextAttemptAt == null || intent.NextAttemptAt <= now) &&
                dbContext.Payments.Any(payment => payment.Id == intent.PaymentId &&
                    payment.Status == PaymentStatus.Succeeded))
            .OrderBy(intent => intent.NextAttemptAt.HasValue)
            .ThenBy(intent => intent.NextAttemptAt)
            .ThenBy(intent => intent.PaymentId)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
    public async Task<bool> ScheduleRetryAsync(
        PaymentId paymentId,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        var utcNextAttempt = nextAttemptAt.ToUniversalTime();
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var eventId = Guid.NewGuid();
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH scheduled AS (
                UPDATE payment_ledger_posting_intents
                SET next_attempt_at = {utcNextAttempt}
                WHERE payment_id = {paymentId.Value} AND status = 'Pending'
                RETURNING payment_id, transaction_id
            )
            INSERT INTO payment_ledger_posting_events
                (id, payment_id, transaction_id, kind, occurred_at, next_attempt_at)
            SELECT {eventId}, payment_id, transaction_id, 'RetryScheduled', {now}, {utcNextAttempt}
            FROM scheduled
            """, cancellationToken);
        return affected == 1;
    }
}
