using Microsoft.EntityFrameworkCore;
using Demopay.Modules.Reconciliation.Application;
using Demopay.Modules.Reconciliation.Domain;

namespace Demopay.Modules.Reconciliation.Infrastructure.Persistence;

public sealed class EfReconciliationRepository(
    ReconciliationDbContext dbContext)
    : IReconciliationRepository
{
    public async Task AddAsync(
        ReconciliationRecord record,
        CancellationToken cancellationToken = default)
    {
        dbContext.ReconciliationRecords.Add(record);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReconciliationRecord?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ReconciliationRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == id,
                cancellationToken);
    }
}
