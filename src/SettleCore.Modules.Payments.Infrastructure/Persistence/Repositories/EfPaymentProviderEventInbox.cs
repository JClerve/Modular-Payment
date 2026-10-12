using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentProviderEventInbox(PaymentsDbContext dbContext, TimeProvider clock) : IPaymentProviderEventInbox
{
    public async Task<IReadOnlyList<ProviderPaymentSucceededEvent>> GetPendingAsync(string provider, int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        var now = clock.GetUtcNow();
        var receipts = await dbContext.PaymentProviderEventReceipts.AsNoTracking()
            .Where(x => x.Provider == provider && x.ProcessedAt == null &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.EventId).Take(batchSize).ToListAsync(cancellationToken);
        return receipts.Select(x => x.ToEvent()).ToArray();
    }

    public async Task<bool> ScheduleRetryAsync(string provider, string eventId, DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var due = nextAttemptAt.ToUniversalTime();
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE payment_provider_event_receipts SET next_attempt_at = {due}
            WHERE provider = {provider} AND event_id = {eventId} AND processed_at IS NULL
            """, cancellationToken) == 1;
    }

    public async Task<bool> ReceiveAsync(ProviderPaymentSucceededEvent evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.EventId);
        var request = new CreateProviderPaymentRequest(evidence.PaymentId, evidence.AmountMinorUnits, evidence.Currency);
        var reference = Domain.ProviderPaymentReference.Create(evidence.ProviderReference.Provider, evidence.ProviderReference.Reference);
        var normalized = evidence with { Currency = request.Currency, ProviderReference = reference, OccurredAt = evidence.OccurredAt.ToUniversalTime() };
        var receivedAt = clock.GetUtcNow().ToUniversalTime();
        var count = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment_provider_event_receipts
                (provider, event_id, payment_id, provider_reference, amount_minor_units, currency, occurred_at, is_live_mode, received_at)
            VALUES ({reference.Provider}, {normalized.EventId}, {request.PaymentId.Value}, {reference.Reference},
                {request.AmountMinorUnits}, {request.Currency}, {normalized.OccurredAt}, {normalized.IsLiveMode}, {receivedAt})
            ON CONFLICT (provider, event_id) DO NOTHING
            """, cancellationToken);
        if (count == 1)
        {
            return true;
        }

        var existing = await dbContext.PaymentProviderEventReceipts.AsNoTracking().SingleAsync(
            x => x.Provider == reference.Provider && x.EventId == normalized.EventId, cancellationToken);
        if (existing.ToEvent() != normalized)
        {
            throw new ProviderPaymentEventConflictException();
        }
        return false;
    }
}
