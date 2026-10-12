using Microsoft.EntityFrameworkCore;
using Npgsql;
using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence;

public sealed class EfLedgerRepository(
    LedgerDbContext dbContext)
    : ILedgerRepository
{
    public async Task<IReadOnlyList<LedgerAccount>>
        GetAccountsByIdsAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return [];
        }

        var ids = accountIds
            .Distinct()
            .ToArray();

        return await dbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<LedgerTransaction?> GetTransactionByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var record =
            await dbContext.LedgerTransactions
                .AsNoTracking()
                .Include(transaction => transaction.Entries)
                .SingleOrDefaultAsync(
                    transaction =>
                        transaction.Id == transactionId,
                    cancellationToken);

        if (record is null)
        {
            return null;
        }

        var entries = record.Entries
            .Select(entry =>
                LedgerEntry.Create(
                    entry.AccountId,
                    entry.Currency,
                    entry.Direction,
                    entry.AmountMinorUnits))
            .ToArray();

        return LedgerTransaction.Rehydrate(
            record.Id,
            record.LedgerId,
            entries);
    }

    public async Task AddTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var existing = await GetTransactionByIdAsync(transaction.Id, cancellationToken);
        if (existing is not null)
        {
            EnsureSamePosting(existing, transaction);
            return;
        }

        var entries = transaction.Entries
            .Select(entry =>
                new LedgerEntryRecord(
                    Guid.NewGuid(),
                    transaction.Id,
                    entry.AccountId,
                    entry.Currency,
                    entry.Direction,
                    entry.AmountMinorUnits))
            .ToArray();

        var record = new LedgerTransactionRecord(
            transaction.Id,
            transaction.LedgerId,
            entries);

        dbContext.LedgerTransactions.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "PK_ledger_transactions"
            })
        {
            // Another writer won. Detach only this failed insert, leaving unrelated tracking intact.
            foreach (var entry in entries)
            {
                dbContext.Entry(entry).State = EntityState.Detached;
            }
            dbContext.Entry(record).State = EntityState.Detached;

            existing = await GetTransactionByIdAsync(transaction.Id, cancellationToken);
            if (existing is null)
            {
                throw;
            }
            EnsureSamePosting(existing, transaction);
        }
    }

    private static void EnsureSamePosting(LedgerTransaction existing, LedgerTransaction requested)
    {
        // Compare a multiset: database row order is irrelevant, duplicate entries are not.
        static IEnumerable<(Guid AccountId, string Currency, LedgerDirection Direction, long Amount)> Contents(LedgerTransaction transaction) =>
            transaction.Entries
                .Select(entry => (entry.AccountId, entry.Currency, entry.Direction, entry.AmountMinorUnits))
                .OrderBy(entry => entry.AccountId)
                .ThenBy(entry => entry.Currency, StringComparer.Ordinal)
                .ThenBy(entry => entry.Direction)
                .ThenBy(entry => entry.AmountMinorUnits);

        if (existing.LedgerId != requested.LedgerId || !Contents(existing).SequenceEqual(Contents(requested)))
        {
            throw new LedgerTransactionConflictException(requested.Id);
        }
    }
}
