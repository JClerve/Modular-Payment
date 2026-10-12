namespace SettleCore.Modules.Payments.Domain;

public static class PaymentCurrencyPrecision
{
    public static int GetDecimalPlaces(
        string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (string.IsNullOrWhiteSpace(currency) ||
            currency.Length != 3 ||
            !currency.All(char.IsLetter))
        {
            throw new ArgumentException(
                "Payment currency must be a three-letter alphabetic code.",
                nameof(currency));
        }

        var normalizedCurrency =
            currency.ToUpperInvariant();

        return normalizedCurrency switch
        {
            "SGD" => 2,
            "JPY" => 0,
            "KWD" => 3,

            _ => throw new ArgumentException(
                $"Payment currency '{normalizedCurrency}' is not supported.",
                nameof(currency))
        };
    }
}