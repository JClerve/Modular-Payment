using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Application;

public interface IReconciliationRepository
{
    Task AddAsync(
        ReconciliationRecord record,
        CancellationToken cancellationToken = default);

    Task<ReconciliationRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
