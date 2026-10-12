using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentLedgerPostingPreparationRepository(PaymentsDbContext context)
    : IPaymentLedgerPostingPreparationRepository
{
    public async Task SaveAsync(PaymentLedgerPostingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment_ledger_posting_preparations
                (payment_id, transaction_id, ledger_id, processor_receivable_account_id,
                 merchant_payable_account_id, platform_revenue_account_id, currency,
                 gross_amount_minor_units, fee_amount_minor_units)
            VALUES ({request.PaymentId}, {request.TransactionId}, {request.LedgerId},
                {request.ProcessorReceivableAccountId}, {request.MerchantPayableAccountId},
                {request.PlatformRevenueAccountId}, {request.Currency},
                {request.GrossAmountMinorUnits}, {request.FeeAmountMinorUnits})
            ON CONFLICT (payment_id) DO NOTHING
            """, cancellationToken);
        var stored = await GetByPaymentIdAsync(PaymentId.From(request.PaymentId), cancellationToken);
        if (stored != request) throw new PaymentLedgerPostingIntentConflictException(PaymentId.From(request.PaymentId));
    }

    public async Task<PaymentLedgerPostingRequest?> GetByPaymentIdAsync(PaymentId paymentId,
        CancellationToken cancellationToken = default)
    {
        var preparation = await context.PaymentLedgerPostingPreparations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);
        return preparation?.ToRequest();
    }
}
