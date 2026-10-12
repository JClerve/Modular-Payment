using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Application;

public interface ILedgerRepository
{
    Task<IReadOnlyList<LedgerAccount>> GetAccountsByIdsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken = default);

    Task<LedgerTransaction?> GetTransactionByIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    // Repeating an ID with the same ledger and entry multiset is a no-op.
    // Different contents throw LedgerTransactionConflictException, including concurrent retries.
    Task AddTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken = default);
}
