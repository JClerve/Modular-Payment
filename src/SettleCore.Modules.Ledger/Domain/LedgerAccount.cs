namespace SettleCore.Modules.Ledger.Domain;

public sealed class LedgerAccount
{
    private LedgerAccount(Guid id, Guid ledgerId, string currency)
    {
        Id = id;
        LedgerId = ledgerId;
        Currency = currency;
    }

    public Guid Id { get; }

    public Guid LedgerId { get; }

    public string Currency { get; }

    public static LedgerAccount Open(
        Guid id,
        Guid ledgerId,
        string currency)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger account ID must not be empty.",
                nameof(id));
        }

        if (ledgerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger ID must not be empty.",
                nameof(ledgerId));
        }

        ArgumentNullException.ThrowIfNull(currency);

        if (currency.Length != 3 ||
            !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Ledger account currency must be a three-letter alphabetic code.",
                nameof(currency));
        }

        return new LedgerAccount(
            id,
            ledgerId,
            currency.ToUpperInvariant());
    }
}
