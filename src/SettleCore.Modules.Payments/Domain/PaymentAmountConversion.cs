namespace SettleCore.Modules.Payments.Domain;

public static class PaymentAmountConversion
{
    public static long ToMinorUnits(decimal amount, string currency)
    {
        return ToMinorUnits(amount, PaymentCurrencyPrecision.GetDecimalPlaces(currency));
    }

    // Precision must come from an explicit currency policy; never assume two decimals.
    public static long ToMinorUnits(decimal amount, int decimalPlaces)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (decimalPlaces is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Precision must be between zero and 28.");
        }

        decimal factor = 1;
        for (var i = 0; i < decimalPlaces; i++)
        {
            factor *= 10;
        }

        decimal minorUnits;
        try
        {
            minorUnits = checked(amount * factor);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount exceeds the supported minor-unit range.");
        }

        if (minorUnits > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount exceeds the supported minor-unit range.");
        }

        if (minorUnits != decimal.Truncate(minorUnits))
        {
            throw new ArgumentException("Amount contains fractional minor units.", nameof(amount));
        }

        return (long)minorUnits;
    }
}
