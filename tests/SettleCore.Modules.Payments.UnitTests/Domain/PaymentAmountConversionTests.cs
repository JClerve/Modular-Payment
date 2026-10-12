using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentAmountConversionTests
{
    [Theory]
    [InlineData("123", 0, 123L)]
    [InlineData("123.45", 2, 12345L)]
    [InlineData("123.456", 3, 123456L)]
    [InlineData("1.2345", 4, 12345L)]
    [InlineData("1.2300", 2, 123L)]
    [InlineData("0.0000000000000000000000000001", 28, 1L)]
    [InlineData("92233720368547758.07", 2, long.MaxValue)]
    public void ConvertsExactly(string amount, int decimalPlaces, long expected)
    {
        Assert.Equal(expected, PaymentAmountConversion.ToMinorUnits(Parse(amount), decimalPlaces));
    }

    [Theory]
    [InlineData("1.1", 0)]
    [InlineData("1.001", 2)]
    [InlineData("0.0001", 3)]
    public void RejectsFractionalMinorUnits(string amount, int decimalPlaces)
    {
        Assert.Throws<ArgumentException>(() => PaymentAmountConversion.ToMinorUnits(Parse(amount), decimalPlaces));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void RejectsNonPositiveAmounts(string amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentAmountConversion.ToMinorUnits(Parse(amount), 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public void RejectsInvalidPrecision(int decimalPlaces)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentAmountConversion.ToMinorUnits(1m, decimalPlaces));
    }

    [Theory]
    [InlineData("92233720368547758.08", 2)]
    [InlineData("79228162514264337593543950335", 2)]
    public void RejectsOverflow(string amount, int decimalPlaces)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentAmountConversion.ToMinorUnits(Parse(amount), decimalPlaces));
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
