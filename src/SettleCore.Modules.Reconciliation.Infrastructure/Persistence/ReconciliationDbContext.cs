using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.Infrastructure.Persistence;

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
