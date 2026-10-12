namespace Demopay.Modules.Ledger.Domain;

public sealed class LedgerTransactionConflictException(Guid transactionId)
    : InvalidOperationException($"Ledger transaction '{transactionId}' already exists with different posting contents.")
{
    public Guid TransactionId { get; } = transactionId;
}
