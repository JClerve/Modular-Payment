using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext(
    DbContextOptions<LedgerDbContext> options)
    : DbContext(options)
{
    public DbSet<LedgerAccount> LedgerAccounts =>
        Set<LedgerAccount>();

    public DbSet<LedgerTransactionRecord> LedgerTransactions =>
        Set<LedgerTransactionRecord>();

    public DbSet<LedgerEntryRecord> LedgerEntries =>
        Set<LedgerEntryRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(LedgerDbContext).Assembly);
    }
}
