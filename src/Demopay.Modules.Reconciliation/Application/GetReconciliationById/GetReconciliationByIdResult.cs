using Demopay.Modules.Reconciliation.Domain;

namespace Demopay.Modules.Reconciliation.Application.GetReconciliationById;

public sealed record GetReconciliationByIdResult(
    Guid ReconciliationId,
    ReconciliationStatus Status,
    decimal ExpectedAmount,
    decimal ActualAmount,
    string ExpectedCurrency,
    string ActualCurrency);
