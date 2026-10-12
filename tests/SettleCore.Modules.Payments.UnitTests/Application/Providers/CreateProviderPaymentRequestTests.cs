using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.Providers;

public sealed class CreateProviderPaymentRequestTests
{
    [Fact]
    public void RetryUsesStablePaymentIdentityAndPreservesExplicitAmount()
    {
        var id = PaymentId.New();
        var first = new CreateProviderPaymentRequest(id, 1234, "sgd");
        var retry = new CreateProviderPaymentRequest(id, 1234, "SGD");
        var other = new CreateProviderPaymentRequest(PaymentId.New(), 1234, "SGD");

        Assert.Equal(id, first.PaymentId);
        Assert.Equal(1234, first.AmountMinorUnits);
        Assert.Equal("SGD", first.Currency);
        Assert.Equal($"settlecore:payment:{id.Value:N}:create", first.IdempotencyKey);
        Assert.Equal(first.IdempotencyKey, retry.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, other.IdempotencyKey);
    }

    [Fact]
    public void EmptyPaymentIdentityIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CreateProviderPaymentRequest(PaymentId.Empty, 1234, "SGD"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveAmountIsRejected(long amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreateProviderPaymentRequest(PaymentId.New(), amount, "SGD"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("123")]
    public void InvalidCurrencyIsRejected(string currency)
    {
        Assert.Throws<ArgumentException>(() =>
            new CreateProviderPaymentRequest(PaymentId.New(), 1234, currency));
    }

    [Fact]
    public void NullCurrencyIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CreateProviderPaymentRequest(PaymentId.New(), 1234, null!));
    }
}
