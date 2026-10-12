using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Application.CreateReconciliation;

public sealed record CreateReconciliationResult(
    Guid ReconciliationId,
    ReconciliationStatus Status,
    decimal ExpectedAmount,
    decimal ActualAmount,
    string ExpectedCurrency,
    string ActualCurrency);