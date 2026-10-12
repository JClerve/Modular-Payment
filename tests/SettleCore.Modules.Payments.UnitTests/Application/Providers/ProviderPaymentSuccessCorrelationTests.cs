using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.Providers;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.Providers;

public sealed class ProviderPaymentSuccessCorrelationTests
{
    [Fact]
    public void MatchingStoredEvidenceIsAcceptedWithoutChangingPaymentStatus()
    {
        var payment = CreatePayment();
        ProviderPaymentSuccessCorrelation.Validate(payment, Evidence(payment), expectedLiveMode: false);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("provider")]
    [InlineData("reference")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("mode")]
    [InlineData("missing-reference")]
    public void MismatchingStoredEvidenceIsRejected(string mismatch)
    {
        var payment = CreatePayment(attachReference: mismatch != "missing-reference");
        var evidence = Evidence(payment);
        evidence = mismatch switch
        {
            "identity" => evidence with { PaymentId = PaymentId.New() },
            "provider" => evidence with { ProviderReference = ProviderPaymentReference.Create("other", "pi_test") },
            "reference" => evidence with { ProviderReference = ProviderPaymentReference.Create("stripe", "pi_other") },
            "amount" => evidence with { AmountMinorUnits = 1235 },
            "currency" => evidence with { Currency = "JPY" },
            "mode" => evidence with { IsLiveMode = true },
            _ => evidence
        };
        Assert.Throws<ProviderPaymentCorrelationException>(() =>
            ProviderPaymentSuccessCorrelation.Validate(payment, evidence, expectedLiveMode: false));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [InlineData("JPY", 1234, 1234)]
    [InlineData("KWD", 1.234, 1234)]
    public void CorrelationUsesStoredCurrencyPrecision(string currency, decimal amount, long minorUnits)
    {
        var payment = Payment.Create(amount, currency);
        payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        ProviderPaymentSuccessCorrelation.Validate(payment,
            Evidence(payment) with { Currency = currency, AmountMinorUnits = minorUnits, IsLiveMode = true },
            expectedLiveMode: true);
    }

    [Fact]
    public void UnsupportedStoredCurrencyDoesNotAuthorizeSuccess()
    {
        var payment = Payment.Create(12.34m, "USD");
        payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        Assert.Throws<ArgumentException>(() =>
            ProviderPaymentSuccessCorrelation.Validate(payment, Evidence(payment) with { Currency = "USD" }, false));
    }

    private static Payment CreatePayment(bool attachReference = true)
    {
        var payment = Payment.Create(12.34m, "SGD");
        if (attachReference) payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        return payment;
    }

    private static ProviderPaymentSucceededEvent Evidence(Payment payment) => new(
        "evt_test", payment.Id, ProviderPaymentReference.Create("stripe", "pi_test"),
        1234, "SGD", DateTimeOffset.UtcNow, false);
}
