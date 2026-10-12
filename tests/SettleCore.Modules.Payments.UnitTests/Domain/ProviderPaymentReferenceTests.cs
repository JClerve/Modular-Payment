using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class ProviderPaymentReferenceTests
{
    [Fact]
    public void CreatePreservesProviderPaymentReference()
    {
        var reference = ProviderPaymentReference.Create(
            "stripe",
            "pi_3ABC123");

        Assert.Equal("stripe", reference.Provider);
        Assert.Equal("pi_3ABC123", reference.Reference);
    }

    [Fact]
    public void CreateTrimsProviderAndReference()
    {
        var reference = ProviderPaymentReference.Create(
            "  stripe  ",
            "  pi_3ABC123  ");

        Assert.Equal("stripe", reference.Provider);
        Assert.Equal("pi_3ABC123", reference.Reference);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateWithBlankProviderThrows(
        string provider)
    {
        Assert.Throws<ArgumentException>(
            () => ProviderPaymentReference.Create(
                provider,
                "pi_3ABC123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateWithBlankReferenceThrows(
        string reference)
    {
        Assert.Throws<ArgumentException>(
            () => ProviderPaymentReference.Create(
                "stripe",
                reference));
    }
}