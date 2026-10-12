namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed record PaymentLedgerPostingInput(
    Guid TransactionId,
    Guid LedgerId,
    Guid ProcessorReceivableAccountId,
    Guid MerchantPayableAccountId,
    Guid PlatformRevenueAccountId,
    long FeeAmountMinorUnits);