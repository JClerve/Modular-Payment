using Microsoft.EntityFrameworkCore;
using Npgsql;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentSuccessPersistence(
    PaymentsDbContext dbContext, TimeProvider? timeProvider = null)
    : IPaymentSuccessPersistence
{
    public async Task SaveAsync(
        Payment payment,
        PaymentLedgerPostingIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(intent);

        if (payment.Id != intent.PaymentId)
        {
            throw new ArgumentException(
                "Posting intent must belong to the payment.", nameof(intent));
        }

        if (payment.Status != PaymentStatus.Succeeded)
        {
            throw new ArgumentException(
                "Payment must be succeeded.", nameof(payment));
        }

        var existing = await dbContext.PaymentLedgerPostingIntents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PaymentId == intent.PaymentId, cancellationToken);
        if (existing is not null)
        {
            dbContext.Entry(payment).State = EntityState.Detached;
            EnsureSamePosting(existing, intent);
            await EnsurePersistedSuccessAsync(payment.Id, cancellationToken);
            return;
        }

        dbContext.Payments.Update(payment);
        await dbContext.PaymentLedgerPostingIntents.AddAsync(intent, cancellationToken);
        var audit = new PaymentLedgerPostingEvent(Guid.NewGuid(), intent.PaymentId,
            intent.TransactionId, PaymentLedgerPostingEventKind.IntentRecorded,
            (timeProvider ?? TimeProvider.System).GetUtcNow(), null);
        await dbContext.PaymentLedgerPostingEvents.AddAsync(audit, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_payment_ledger_posting_intents"
            })
        {
            // The failed transaction rolled back all three writes. Discard this attempt.
            dbContext.Entry(audit).State = EntityState.Detached;
            dbContext.Entry(intent).State = EntityState.Detached;
            dbContext.Entry(payment).State = EntityState.Detached;
            existing = await dbContext.PaymentLedgerPostingIntents.AsNoTracking()
                .SingleOrDefaultAsync(x => x.PaymentId == intent.PaymentId, cancellationToken);
            if (existing is null)
            {
                throw;
            }
            EnsureSamePosting(existing, intent);
            await EnsurePersistedSuccessAsync(payment.Id, cancellationToken);
        }
    }

    private async Task EnsurePersistedSuccessAsync(
        PaymentId paymentId, CancellationToken cancellationToken)
    {
        var status = await dbContext.Payments.AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Select(x => (PaymentStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (status != PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException(
                "Existing posting intent requires committed payment success.");
        }
    }

    private static void EnsureSamePosting(
        PaymentLedgerPostingIntent existing,
        PaymentLedgerPostingIntent intent)
    {
        if (existing.TransactionId != intent.TransactionId ||
            existing.LedgerId != intent.LedgerId ||
            existing.ProcessorReceivableAccountId != intent.ProcessorReceivableAccountId ||
            existing.MerchantPayableAccountId != intent.MerchantPayableAccountId ||
            existing.PlatformRevenueAccountId != intent.PlatformRevenueAccountId ||
            existing.Currency != intent.Currency ||
            existing.GrossAmountMinorUnits != intent.GrossAmountMinorUnits ||
            existing.FeeAmountMinorUnits != intent.FeeAmountMinorUnits)
        {
            throw new PaymentLedgerPostingIntentConflictException(intent.PaymentId);
        }
    }
}
