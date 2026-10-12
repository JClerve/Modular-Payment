using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Application.ProvisionLedgerAccount;

public interface ILedgerAccountProvisioning
{
    Task ProvisionAsync(LedgerAccount account, CancellationToken cancellationToken = default);
}

public sealed class LedgerAccountConflictException(Guid accountId)
    : InvalidOperationException($"Ledger account {accountId} already has a different ledger or currency.");

public sealed record ProvisionLedgerAccountCommand(Guid AccountId, Guid LedgerId, string Currency);
public sealed record ProvisionLedgerAccountResult(Guid AccountId, Guid LedgerId, string Currency);

public sealed class ProvisionLedgerAccountHandler(ILedgerAccountProvisioning provisioning)
{
    public async Task<ProvisionLedgerAccountResult> HandleAsync(
        ProvisionLedgerAccountCommand command, CancellationToken cancellationToken = default)
    {
        var account = LedgerAccount.Open(command.AccountId, command.LedgerId, command.Currency);
        await provisioning.ProvisionAsync(account, cancellationToken);
        return new(account.Id, account.LedgerId, account.Currency);
    }
}
