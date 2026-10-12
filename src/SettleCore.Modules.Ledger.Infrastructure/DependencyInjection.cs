using SettleCore.Modules.Ledger.Application.ProvisionLedgerAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;
using SettleCore.Modules.Ledger.Application.PostLedgerTransaction;
using SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;

namespace SettleCore.Modules.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Ledger")
            ?? throw new InvalidOperationException(
                "Connection string 'Ledger' is not configured.");

        services.AddDbContext<LedgerDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddScoped<
            ILedgerRepository,
            EfLedgerRepository>();

        services.AddScoped<ILedgerAccountProvisioning, EfLedgerAccountProvisioning>();
        services.AddScoped<ProvisionLedgerAccountHandler>();
        services.AddScoped<PostLedgerTransactionHandler>();
        services.AddScoped<PostPaymentLedgerTransactionHandler>();
        services.AddScoped<GetLedgerTransactionByIdHandler>();

        return services;
    }
}
