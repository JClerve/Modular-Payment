namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed record PaymentLedgerPostingRequest(
    Guid PaymentId,
    Guid TransactionId,
    Guid LedgerId,
    Guid ProcessorReceivableAccountId,
    Guid MerchantPayableAccountId,
    Guid PlatformRevenueAccountId,
    string Currency,
    long GrossAmountMinorUnits,
    long FeeAmountMinorUnits);