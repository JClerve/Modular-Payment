namespace SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;

public sealed record PostPaymentLedgerTransactionCommand(
    Guid TransactionId,
    Guid LedgerId,
    Guid ProcessorReceivableAccountId,
    Guid MerchantPayableAccountId,
    Guid PlatformRevenueAccountId,
    string Currency,
    long GrossAmountMinorUnits,
    long FeeAmountMinorUnits);