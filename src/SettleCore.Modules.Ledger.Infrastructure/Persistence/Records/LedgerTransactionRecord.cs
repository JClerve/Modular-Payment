namespace SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

public sealed class LedgerTransactionRecord
{
    private LedgerTransactionRecord()
    {
    }

    public LedgerTransactionRecord(
        Guid id,
        Guid ledgerId,
        IEnumerable<LedgerEntryRecord> entries)
    {
        Id = id;
        LedgerId = ledgerId;
        Entries = entries.ToList();
    }

    public Guid Id { get; private set; }

    public Guid LedgerId { get; private set; }

    public List<LedgerEntryRecord> Entries { get; private set; } = [];
}