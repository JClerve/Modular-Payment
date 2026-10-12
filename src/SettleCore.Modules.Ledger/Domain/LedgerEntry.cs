namespace SettleCore.Modules.Ledger.Domain;

public sealed class LedgerEntry
{
    private LedgerEntry(
        Guid accountId,
        string currency,
        LedgerDirection direction,
        long amountMinorUnits)
    {
        AccountId = accountId;
        Currency = currency;
        Direction = direction;
        AmountMinorUnits = amountMinorUnits;
    }

    public Guid AccountId { get; }

    public string Currency { get; }

    public LedgerDirection Direction { get; }

    public long AmountMinorUnits { get; }

    public static LedgerEntry Create(
        Guid accountId,
        string currency,
        LedgerDirection direction,
        long amountMinorUnits)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger account ID must not be empty.",
                nameof(accountId));
        }

        ArgumentNullException.ThrowIfNull(currency);

        if (currency.Length != 3 ||
            !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Ledger currency must be a three-letter alphabetic code.",
                nameof(currency));
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (amountMinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountMinorUnits),
                amountMinorUnits,
                "Ledger entry amount must be greater than zero.");
        }

        return new LedgerEntry(
            accountId,
            currency.ToUpperInvariant(),
            direction,
            amountMinorUnits);
    }
}
