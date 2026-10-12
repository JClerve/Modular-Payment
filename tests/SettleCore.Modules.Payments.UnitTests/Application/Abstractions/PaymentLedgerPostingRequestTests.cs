using SettleCore.Modules.Payments.Application.Abstractions;

namespace SettleCore.Modules.Payments.UnitTests.Application.Abstractions;

public sealed class PaymentLedgerPostingRequestTests
{
    [Fact]
    public void CreatePreservesAccountingRequestData()
    {
        var paymentId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var request = new PaymentLedgerPostingRequest(
            PaymentId: paymentId,
            TransactionId: transactionId,
            LedgerId: ledgerId,
            ProcessorReceivableAccountId:
                processorReceivableAccountId,
            MerchantPayableAccountId:
                merchantPayableAccountId,
            PlatformRevenueAccountId:
                platformRevenueAccountId,
            Currency: "SGD",
            GrossAmountMinorUnits: 10_000,
            FeeAmountMinorUnits: 300);

        Assert.Equal(paymentId, request.PaymentId);
        Assert.Equal(transactionId, request.TransactionId);
        Assert.Equal(ledgerId, request.LedgerId);

        Assert.Equal(
            processorReceivableAccountId,
            request.ProcessorReceivableAccountId);

        Assert.Equal(
            merchantPayableAccountId,
            request.MerchantPayableAccountId);

        Assert.Equal(
            platformRevenueAccountId,
            request.PlatformRevenueAccountId);

        Assert.Equal("SGD", request.Currency);
        Assert.Equal(10_000, request.GrossAmountMinorUnits);
        Assert.Equal(300, request.FeeAmountMinorUnits);
    }
}