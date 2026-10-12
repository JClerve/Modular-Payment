using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

public sealed class LedgerEntryRecord
{
    private LedgerEntryRecord()
    {
    }

    public LedgerEntryRecord(
        Guid id,
        Guid transactionId,
        Guid accountId,
        string currency,
        LedgerDirection direction,
        long amountMinorUnits)
    {
        Id = id;
        TransactionId = transactionId;
        AccountId = accountId;
        Currency = currency;
        Direction = direction;
        AmountMinorUnits = amountMinorUnits;
    }

    public Guid Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Currency { get; private set; } = null!;

    public LedgerDirection Direction { get; private set; }

    public long AmountMinorUnits { get; private set; }
}