using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.MarkPaymentSucceeded;

public sealed class PaymentLedgerPostingRequestFactoryTests
{
    [Fact]
    public void CreatesPostingRequestFromStoredPaymentAndExplicitInputs()
    {
        var payment = Payment.Create(
            123.45m,
            "sgd");

        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var input = new PaymentLedgerPostingInput(
            transactionId,
            ledgerId,
            processorReceivableAccountId,
            merchantPayableAccountId,
            platformRevenueAccountId,
            FeeAmountMinorUnits: 345);

        var request =
            PaymentLedgerPostingRequestFactory.Create(
                payment,
                input);

        Assert.Equal(payment.Id.Value, request.PaymentId);
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
        Assert.Equal(12_345, request.GrossAmountMinorUnits);
        Assert.Equal(345, request.FeeAmountMinorUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveFee(
        long feeAmountMinorUnits)
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input = CreateInput(
            feeAmountMinorUnits);

        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "feeAmountMinorUnits",
            exception.ParamName);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(10_001)]
    public void RejectsFeeNotLessThanGrossAmount(
        long feeAmountMinorUnits)
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input = CreateInput(
            feeAmountMinorUnits);

        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "feeAmountMinorUnits",
            exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyTransactionId()
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input =
            CreateInput(
                transactionId: Guid.Empty);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "transactionId",
            exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyLedgerId()
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input =
            CreateInput(
                ledgerId: Guid.Empty);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "ledgerId",
            exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyProcessorReceivableAccountId()
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input =
            CreateInput(
                processorReceivableAccountId:
                    Guid.Empty);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "processorReceivableAccountId",
            exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyMerchantPayableAccountId()
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input =
            CreateInput(
                merchantPayableAccountId:
                    Guid.Empty);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "merchantPayableAccountId",
            exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyPlatformRevenueAccountId()
    {
        var payment = Payment.Create(
            100m,
            "SGD");

        var input =
            CreateInput(
                platformRevenueAccountId:
                    Guid.Empty);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentLedgerPostingRequestFactory.Create(
                        payment,
                        input));

        Assert.Equal(
            "platformRevenueAccountId",
            exception.ParamName);
    }

    private static PaymentLedgerPostingInput CreateInput(
        long feeAmountMinorUnits = 300,
        Guid? transactionId = null,
        Guid? ledgerId = null,
        Guid? processorReceivableAccountId = null,
        Guid? merchantPayableAccountId = null,
        Guid? platformRevenueAccountId = null)
    {
        return new(
            transactionId ?? Guid.NewGuid(),
            ledgerId ?? Guid.NewGuid(),
            processorReceivableAccountId ?? Guid.NewGuid(),
            merchantPayableAccountId ?? Guid.NewGuid(),
            platformRevenueAccountId ?? Guid.NewGuid(),
            feeAmountMinorUnits);
    }
}