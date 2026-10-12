using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentIdTests
{
    [Fact]
    public void FromExistingGuidPreservesValue()
    {
        var value = Guid.NewGuid();

        var paymentId = PaymentId.From(value);

        Assert.Equal(value, paymentId.Value);
    }

    [Fact]
    public void FromEmptyGuidThrows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => PaymentId.From(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }
}