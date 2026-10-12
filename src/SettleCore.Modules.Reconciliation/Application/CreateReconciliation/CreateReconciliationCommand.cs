namespace SettleCore.Modules.Reconciliation.Application.CreateReconciliation;

public sealed record CreateReconciliationCommand(
    decimal ExpectedAmount,
    string ExpectedCurrency,
    decimal ActualAmount,
    string ActualCurrency);