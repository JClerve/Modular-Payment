using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demopay.Modules.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContextFactory
    : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(
        string[] args)
    {
        var options =
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(
                    "Host=localhost;Port=5432;Database=Demopay;Username=Demopay;Password=Demopay")
                .Options;

        return new LedgerDbContext(options);
    }
}