using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demopay.Modules.Reconciliation.Infrastructure.Persistence;

public sealed class ReconciliationDbContextFactory
    : IDesignTimeDbContextFactory<ReconciliationDbContext>
{
    public ReconciliationDbContext CreateDbContext(
        string[] args)
    {
        var options =
            new DbContextOptionsBuilder<ReconciliationDbContext>()
                .UseNpgsql(
                    "Host=localhost;Port=5432;Database=Demopay;Username=Demopay;Password=Demopay")
                .Options;

        return new ReconciliationDbContext(options);
    }
}