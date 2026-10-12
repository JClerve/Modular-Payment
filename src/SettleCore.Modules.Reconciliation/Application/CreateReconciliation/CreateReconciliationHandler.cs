using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Application.CreateReconciliation;

public sealed class CreateReconciliationHandler(
    IReconciliationRepository repository)
{
    public async Task<CreateReconciliationResult> HandleAsync(
        CreateReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var comparison = ReconciliationComparison.Compare(
            command.ExpectedAmount,
            command.ExpectedCurrency,
            command.ActualAmount,
            command.ActualCurrency);

        var record = new ReconciliationRecord(
            Guid.NewGuid(),
            comparison.Status,
            comparison.ExpectedAmount,
            comparison.ActualAmount,
            comparison.ExpectedCurrency,
            comparison.ActualCurrency);

        await repository.AddAsync(
            record,
            cancellationToken);

        return new CreateReconciliationResult(
            record.Id,
            record.Status,
            record.ExpectedAmount,
            record.ActualAmount,
            record.ExpectedCurrency,
            record.ActualCurrency);
    }
}