namespace SettleCore.Modules.Reconciliation.Application.GetReconciliationById;

public sealed class GetReconciliationByIdHandler(
    IReconciliationRepository repository)
{
    public async Task<GetReconciliationByIdResult?> HandleAsync(
        GetReconciliationByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var record = await repository.GetByIdAsync(
            query.ReconciliationId,
            cancellationToken);

        return record is null
            ? null
            : new GetReconciliationByIdResult(
                record.Id,
                record.Status,
                record.ExpectedAmount,
                record.ActualAmount,
                record.ExpectedCurrency,
                record.ActualCurrency);
    }
}
