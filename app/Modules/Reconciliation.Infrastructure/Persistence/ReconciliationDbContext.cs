using Microsoft.EntityFrameworkCore;
using Demopay.Modules.Reconciliation.Domain;

namespace Demopay.Modules.Reconciliation.Infrastructure.Persistence;

public sealed class ReconciliationDbContext(
    DbContextOptions<ReconciliationDbContext> options)
    : DbContext(options)
{
    public DbSet<ReconciliationRecord> ReconciliationRecords =>
        Set<ReconciliationRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReconciliationDbContext).Assembly);
    }
}
