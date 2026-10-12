using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentLedgerPostingIntentTests
{
    [Fact]
    public void CreateStoresImmutablePostingPayloadAndStartsPending()
    {
        var paymentId = PaymentId.New();
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var intent =
            PaymentLedgerPostingIntent.Create(
                paymentId,
                transactionId,
                ledgerId,
                processorReceivableAccountId,
                merchantPayableAccountId,
                platformRevenueAccountId,
                "SGD",
                grossAmountMinorUnits: 10_000,
                feeAmountMinorUnits: 300);

        Assert.Equal(paymentId, intent.PaymentId);
        Assert.Equal(transactionId, intent.TransactionId);
        Assert.Equal(ledgerId, intent.LedgerId);

        Assert.Equal(
            processorReceivableAccountId,
            intent.ProcessorReceivableAccountId);

        Assert.Equal(
            merchantPayableAccountId,
            intent.MerchantPayableAccountId);

        Assert.Equal(
            platformRevenueAccountId,
            intent.PlatformRevenueAccountId);

        Assert.Equal("SGD", intent.Currency);
        Assert.Equal(10_000, intent.GrossAmountMinorUnits);
        Assert.Equal(300, intent.FeeAmountMinorUnits);

        Assert.Equal(
            PaymentLedgerPostingIntentStatus.Pending,
            intent.Status);
    }
    [Fact]
    public void MarkPostedIsIdempotentAndRetainsPostingPayload()
    {
        var paymentId = PaymentId.New();
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var intent =
            PaymentLedgerPostingIntent.Create(
                paymentId,
                transactionId,
                ledgerId,
                processorReceivableAccountId,
                merchantPayableAccountId,
                platformRevenueAccountId,
                "SGD",
                grossAmountMinorUnits: 10_000,
                feeAmountMinorUnits: 300);

        intent.MarkPosted();
        intent.MarkPosted();

        Assert.Equal(paymentId, intent.PaymentId);
        Assert.Equal(transactionId, intent.TransactionId);
        Assert.Equal(ledgerId, intent.LedgerId);

        Assert.Equal(
            processorReceivableAccountId,
            intent.ProcessorReceivableAccountId);

        Assert.Equal(
            merchantPayableAccountId,
            intent.MerchantPayableAccountId);

        Assert.Equal(
            platformRevenueAccountId,
            intent.PlatformRevenueAccountId);

        Assert.Equal("SGD", intent.Currency);
        Assert.Equal(10_000, intent.GrossAmountMinorUnits);
        Assert.Equal(300, intent.FeeAmountMinorUnits);

        Assert.Equal(
            PaymentLedgerPostingIntentStatus.Posted,
            intent.Status);
    }
}