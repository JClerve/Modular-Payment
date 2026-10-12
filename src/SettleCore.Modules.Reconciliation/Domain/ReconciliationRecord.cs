namespace SettleCore.Modules.Reconciliation.Domain;

public sealed class ReconciliationRecord
{
    private ReconciliationRecord()
    {
    }

    public ReconciliationRecord(
        Guid id,
        ReconciliationStatus status,
        decimal expectedAmount,
        decimal actualAmount,
        string expectedCurrency,
        string actualCurrency)
    {
        Id = id;
        Status = status;
        ExpectedAmount = expectedAmount;
        ActualAmount = actualAmount;
        ExpectedCurrency = expectedCurrency;
        ActualCurrency = actualCurrency;
    }

    public Guid Id { get; private set; }

    public ReconciliationStatus Status { get; private set; }

    public decimal ExpectedAmount { get; private set; }

    public decimal ActualAmount { get; private set; }

    public string ExpectedCurrency { get; private set; } = null!;

    public string ActualCurrency { get; private set; } = null!;
}
