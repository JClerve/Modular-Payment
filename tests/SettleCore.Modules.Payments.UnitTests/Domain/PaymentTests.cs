using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentTests
{
    public static TheoryData<decimal> NonPositiveAmounts =>
        new()
        {
            0m,
            -1m,
            -100m
        };

    [Fact]
    public void CreateWithPositiveAmountCreatesPendingPayment()
    {
        var payment = Payment.Create(100.00m, "SGD");

        Assert.Equal(100.00m, payment.Amount);
        Assert.Equal("SGD", payment.Currency);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void CreateWithNonPositiveAmountThrows(decimal amount)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Payment.Create(amount, "SGD"));

        Assert.Equal("amount", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void CreateWithBlankCurrencyThrows(string currency)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Payment.Create(100.00m, currency));

        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void CreateWithNullCurrencyThrows()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => Payment.Create(100.00m, null!));

        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void CreateWithLowercaseCurrencyNormalizesToUppercase()
    {
        var payment = Payment.Create(100.00m, "sgd");

        Assert.Equal("SGD", payment.Currency);
    }

    [Theory]
    [InlineData("SG")]
    [InlineData("SGDD")]
    [InlineData("123")]
    [InlineData("S$D")]
    public void CreateWithInvalidCurrencyFormatThrows(string currency)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Payment.Create(100.00m, currency));

        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void CreateGeneratesNonEmptyPaymentId()
    {
        var payment = Payment.Create(100.00m, "SGD");

        Assert.NotEqual(PaymentId.Empty, payment.Id);
    }

    [Fact]
    public void RehydratePreservesExistingPaymentState()
    {
        var id = PaymentId.From(Guid.NewGuid());

        var payment = Payment.Rehydrate(
            id,
            100.00m,
            "SGD",
            PaymentStatus.Pending);

        Assert.Equal(id, payment.Id);
        Assert.Equal(100.00m, payment.Amount);
        Assert.Equal("SGD", payment.Currency);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void RehydrateWithEmptyPaymentIdThrows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Payment.Rehydrate(
                PaymentId.Empty,
                100.00m,
                "SGD",
                PaymentStatus.Pending));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void MarkSucceededChangesStatusFromPendingToSucceeded()
    {
        var payment = Payment.Create(100.00m, "SGD");

        payment.MarkSucceeded();

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void MarkSucceededWhenAlreadySucceededThrows()
    {
        var payment = Payment.Create(100.00m, "SGD");

        payment.MarkSucceeded();

        var exception = Assert.Throws<InvalidOperationException>(
            payment.MarkSucceeded);

        Assert.Equal(
            "Payment is already succeeded.",
            exception.Message);
    }

    [Fact]
    public void AttachProviderReferenceAssociatesExternalPaymentIdentity()
    {
        var payment = Payment.Create(100.00m, "SGD");

        var providerReference = ProviderPaymentReference.Create(
            "stripe",
            "pi_3ABC123");

        payment.AttachProviderReference(providerReference);

        Assert.Equal(
            providerReference,
            payment.ProviderReference);
    }
}
