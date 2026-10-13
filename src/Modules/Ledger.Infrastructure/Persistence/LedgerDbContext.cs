using Microsoft.EntityFrameworkCore;
using Demopay.Modules.Ledger.Domain;
using Demopay.Modules.Ledger.Infrastructure.Persistence.Records;

namespace Demopay.Modules.Ledger.Infrastructure.Persistence;

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
