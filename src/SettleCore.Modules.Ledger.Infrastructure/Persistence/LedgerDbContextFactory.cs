using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContextFactory
    : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(
        string[] args)
    {
        var options =
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(
                    "Host=localhost;Port=5432;Database=settlecore;Username=settlecore;Password=settlecore")
                .Options;

        return new LedgerDbContext(options);
    }
}