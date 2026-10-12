using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Ledger.Application.ProvisionLedgerAccount;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Infrastructure.Persistence;

public sealed class EfLedgerAccountProvisioning(LedgerDbContext dbContext) : ILedgerAccountProvisioning
{
    public async Task ProvisionAsync(LedgerAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ledger_accounts (id, ledger_id, currency) VALUES ({account.Id}, {account.LedgerId}, {account.Currency}) ON CONFLICT (id) DO NOTHING",
            cancellationToken);
        var stored = await dbContext.LedgerAccounts.AsNoTracking().SingleAsync(
            existing => existing.Id == account.Id, cancellationToken);
        if (stored.LedgerId != account.LedgerId || stored.Currency != account.Currency)
            throw new LedgerAccountConflictException(account.Id);
    }
}
