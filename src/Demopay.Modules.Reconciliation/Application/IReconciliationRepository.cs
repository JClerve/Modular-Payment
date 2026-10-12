using Demopay.Modules.Reconciliation.Domain;

namespace Demopay.Modules.Reconciliation.Application;

public interface IReconciliationRepository
{
    Task AddAsync(
        ReconciliationRecord record,
        CancellationToken cancellationToken = default);

    Task<ReconciliationRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
