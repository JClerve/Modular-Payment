namespace SettleCore.Modules.Reconciliation.Domain;

public static class ReconciliationComparison
{
    public static ReconciliationResult Compare(
        decimal expectedAmount,
        string expectedCurrency,
        decimal actualAmount,
        string actualCurrency)
    {
        var normalizedExpectedCurrency =
            ValidateAndNormalizeCurrency(
                expectedCurrency,
                nameof(expectedCurrency));

        var normalizedActualCurrency =
            ValidateAndNormalizeCurrency(
                actualCurrency,
                nameof(actualCurrency));

        if (expectedAmount != actualAmount &&
            normalizedExpectedCurrency != normalizedActualCurrency)
        {
            return new ReconciliationResult(
                ReconciliationStatus.Mismatch,
                expectedAmount,
                actualAmount,
                normalizedExpectedCurrency,
                normalizedActualCurrency);
        }

        if (expectedAmount != actualAmount &&
            normalizedExpectedCurrency == normalizedActualCurrency)
        {
            return new ReconciliationResult(
                ReconciliationStatus.AmountMismatch,
                expectedAmount,
                actualAmount,
                normalizedExpectedCurrency,
                normalizedActualCurrency);
        }

        if (expectedAmount == actualAmount &&
            normalizedExpectedCurrency != normalizedActualCurrency)
        {
            return new ReconciliationResult(
                ReconciliationStatus.CurrencyMismatch,
                expectedAmount,
                actualAmount,
                normalizedExpectedCurrency,
                normalizedActualCurrency);
        }

        return new ReconciliationResult(
            ReconciliationStatus.Matched,
            expectedAmount,
            actualAmount,
            normalizedExpectedCurrency,
            normalizedActualCurrency);
    }

    private static string ValidateAndNormalizeCurrency(
        string currency,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Reconciliation currency must not be blank.",
                parameterName);
        }

        if (currency.Length != 3 ||
            !currency.All(char.IsLetter))
        {
            throw new ArgumentException(
                "Reconciliation currency must be a three-letter alphabetic code.",
                parameterName);
        }

        return currency.ToUpperInvariant();
    }
}
