namespace SettleCore.Modules.Payments.Domain;

public sealed class PaymentLedgerPostingIntent
{
    private PaymentLedgerPostingIntent(
        PaymentId paymentId,
        Guid transactionId,
        Guid ledgerId,
        Guid processorReceivableAccountId,
        Guid merchantPayableAccountId,
        Guid platformRevenueAccountId,
        string currency,
        long grossAmountMinorUnits,
        long feeAmountMinorUnits,
        PaymentLedgerPostingIntentStatus status)
    {
        PaymentId = paymentId;
        TransactionId = transactionId;
        LedgerId = ledgerId;
        ProcessorReceivableAccountId =
            processorReceivableAccountId;
        MerchantPayableAccountId =
            merchantPayableAccountId;
        PlatformRevenueAccountId =
            platformRevenueAccountId;
        Currency = currency;
        GrossAmountMinorUnits =
            grossAmountMinorUnits;
        FeeAmountMinorUnits =
            feeAmountMinorUnits;
        Status = status;
    }

    public PaymentId PaymentId { get; }

    public Guid TransactionId { get; }

    public Guid LedgerId { get; }

    public Guid ProcessorReceivableAccountId { get; }

    public Guid MerchantPayableAccountId { get; }

    public Guid PlatformRevenueAccountId { get; }

    public string Currency { get; }

    public long GrossAmountMinorUnits { get; }

    public long FeeAmountMinorUnits { get; }

    public PaymentLedgerPostingIntentStatus Status { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public void MarkPosted()
    {
        Status = PaymentLedgerPostingIntentStatus.Posted;
    }

    public static PaymentLedgerPostingIntent Create(
        PaymentId paymentId,
        Guid transactionId,
        Guid ledgerId,
        Guid processorReceivableAccountId,
        Guid merchantPayableAccountId,
        Guid platformRevenueAccountId,
        string currency,
        long grossAmountMinorUnits,
        long feeAmountMinorUnits)
    {
        return new PaymentLedgerPostingIntent(
            paymentId,
            transactionId,
            ledgerId,
            processorReceivableAccountId,
            merchantPayableAccountId,
            platformRevenueAccountId,
            currency,
            grossAmountMinorUnits,
            feeAmountMinorUnits,
            PaymentLedgerPostingIntentStatus.Pending);
    }
}
