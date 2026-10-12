using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Application.Providers;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentProviderEventProcessor(PaymentsDbContext context, TimeProvider clock)
    : IPaymentProviderEventProcessor
{
    public async Task<bool> ProcessAsync(string provider, string eventId, bool expectedLiveMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Lock receipt first, then payment: same-event retries and distinct events for one payment serialize.
            var receipts = await context.PaymentProviderEventReceipts.FromSqlInterpolated($"""
                SELECT * FROM payment_provider_event_receipts
                WHERE provider = {provider} AND event_id = {eventId} FOR UPDATE
                """).AsNoTracking().ToListAsync(cancellationToken);
            var receipt = receipts.SingleOrDefault();
            if (receipt is null) return false;
            if (receipt.ProcessedAt.HasValue) return true;

            var lockedIds = await context.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM payments WHERE id = {receipt.PaymentId} FOR UPDATE
                """).ToListAsync(cancellationToken);
            if (lockedIds.Count == 0) return false;
            var paymentId = PaymentId.From(receipt.PaymentId);
            var payment = await context.Payments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
            if (payment is null || payment.ProviderReference is null) return false;
            ProviderPaymentSuccessCorrelation.Validate(payment, receipt.ToEvent(), expectedLiveMode);
            var prepared = await new EfPaymentLedgerPostingPreparationRepository(context)
                .GetByPaymentIdAsync(payment.Id, cancellationToken);
            if (prepared is null) return false;

            var validated = PaymentLedgerPostingRequestFactory.Create(payment, new PaymentLedgerPostingInput(
                prepared.TransactionId, prepared.LedgerId, prepared.ProcessorReceivableAccountId,
                prepared.MerchantPayableAccountId, prepared.PlatformRevenueAccountId, prepared.FeeAmountMinorUnits));
            if (validated != prepared) throw new ProviderPaymentCorrelationException();
            var intent = PaymentLedgerPostingIntent.Create(payment.Id, prepared.TransactionId, prepared.LedgerId,
                prepared.ProcessorReceivableAccountId, prepared.MerchantPayableAccountId,
                prepared.PlatformRevenueAccountId, prepared.Currency, prepared.GrossAmountMinorUnits, prepared.FeeAmountMinorUnits);
            if (payment.Status != PaymentStatus.Succeeded) payment.MarkSucceeded();
            await new EfPaymentSuccessPersistence(context, clock).SaveAsync(payment, intent, cancellationToken);
            var processedAt = clock.GetUtcNow().ToUniversalTime();
            var acknowledged = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE payment_provider_event_receipts SET processed_at = {processedAt}, next_attempt_at = NULL
                WHERE provider = {provider} AND event_id = {eventId} AND processed_at IS NULL
                """, cancellationToken);
            if (acknowledged != 1) throw new InvalidOperationException("Provider receipt acknowledgment was not persisted.");
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
