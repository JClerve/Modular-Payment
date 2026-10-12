using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Application.GetReconciliationById;

public sealed record GetReconciliationByIdResult(
    Guid ReconciliationId,
    ReconciliationStatus Status,
    decimal ExpectedAmount,
    decimal ActualAmount,
    string ExpectedCurrency,
    string ActualCurrency);
