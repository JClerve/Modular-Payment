using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentLedgerPostingPreparation
{
    private PaymentLedgerPostingPreparation() { }
    public PaymentId PaymentId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid LedgerId { get; private set; }
    public Guid ProcessorReceivableAccountId { get; private set; }
    public Guid MerchantPayableAccountId { get; private set; }
    public Guid PlatformRevenueAccountId { get; private set; }
    public string Currency { get; private set; } = null!;
    public long GrossAmountMinorUnits { get; private set; }
    public long FeeAmountMinorUnits { get; private set; }

    public PaymentLedgerPostingRequest ToRequest() => new(PaymentId.Value,
        TransactionId, LedgerId, ProcessorReceivableAccountId, MerchantPayableAccountId, PlatformRevenueAccountId, Currency, GrossAmountMinorUnits, FeeAmountMinorUnits);
}
