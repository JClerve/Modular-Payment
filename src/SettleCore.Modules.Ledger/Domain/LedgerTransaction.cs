using System.Numerics;

namespace SettleCore.Modules.Ledger.Domain;

public sealed class LedgerTransaction
{
    private LedgerTransaction(
        Guid id,
        Guid ledgerId,
        IReadOnlyList<LedgerEntry> entries)
    {
        Id = id;
        LedgerId = ledgerId;
        Entries = entries;
    }

    public Guid Id { get; }

    public Guid LedgerId { get; }

    public IReadOnlyList<LedgerEntry> Entries { get; }

    public static LedgerTransaction Post(
        Guid id,
        Guid ledgerId,
        IEnumerable<LedgerEntry> entries,
        IEnumerable<LedgerAccount> accounts)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger transaction ID must not be empty.",
                nameof(id));
        }

        if (ledgerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger ID must not be empty.",
                nameof(ledgerId));
        }

        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(accounts);

        var entrySnapshot = entries.ToArray();

        if (entrySnapshot.Length < 2 ||
            entrySnapshot.Any(entry => entry is null))
        {
            throw new ArgumentException(
                "Ledger transaction requires at least two valid entries.",
                nameof(entries));
        }

        var accountSnapshot = accounts.ToArray();

        if (accountSnapshot.Any(account => account is null))
        {
            throw new ArgumentException(
                "Ledger accounts must not contain null values.",
                nameof(accounts));
        }

        var accountsById = accountSnapshot.ToDictionary(
            account => account.Id);

        foreach (var entry in entrySnapshot)
        {
            if (!accountsById.TryGetValue(
                    entry.AccountId,
                    out var account))
            {
                throw new ArgumentException(
                    $"Ledger account '{entry.AccountId}' was not provided.",
                    nameof(accounts));
            }

            if (account.LedgerId != ledgerId)
            {
                throw new ArgumentException(
                    $"Ledger account '{account.Id}' does not belong to ledger '{ledgerId}'.",
                    nameof(accounts));
            }

            if (!string.Equals(
                    account.Currency,
                    entry.Currency,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Ledger entry currency '{entry.Currency}' does not match account currency '{account.Currency}'.",
                    nameof(entries));
            }
        }

        foreach (var currencyEntries in entrySnapshot.GroupBy(
                     entry => entry.Currency,
                     StringComparer.Ordinal))
        {
            BigInteger debits = 0;
            BigInteger credits = 0;

            foreach (var entry in currencyEntries)
            {
                if (entry.Direction == LedgerDirection.Debit)
                {
                    debits += entry.AmountMinorUnits;
                }
                else
                {
                    credits += entry.AmountMinorUnits;
                }
            }

            if (debits != credits)
            {
                throw new ArgumentException(
                    $"Ledger entries are not balanced for {currencyEntries.Key}.",
                    nameof(entries));
            }
        }

        return new LedgerTransaction(
            id,
            ledgerId,
            Array.AsReadOnly(entrySnapshot));
    }

    public static LedgerTransaction Rehydrate(
        Guid id,
        Guid ledgerId,
        IEnumerable<LedgerEntry> entries)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger transaction ID must not be empty.",
                nameof(id));
        }

        if (ledgerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ledger ID must not be empty.",
                nameof(ledgerId));
        }

        ArgumentNullException.ThrowIfNull(entries);

        var entrySnapshot = entries.ToArray();

        if (entrySnapshot.Length < 2 ||
            entrySnapshot.Any(entry => entry is null))
        {
            throw new ArgumentException(
                "Ledger transaction requires at least two valid entries.",
                nameof(entries));
        }

        return new LedgerTransaction(
            id,
            ledgerId,
            Array.AsReadOnly(entrySnapshot));
    }
}
