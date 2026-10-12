using Demopay.Modules.Ledger.Application.ProvisionLedgerAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Demopay.Modules.Ledger.Application;
using Demopay.Modules.Ledger.Application.GetLedgerTransactionById;
using Demopay.Modules.Ledger.Application.PostLedgerTransaction;
using Demopay.Modules.Ledger.Application.PostPaymentLedgerTransaction;
using Demopay.Modules.Ledger.Infrastructure.Persistence;

namespace Demopay.Modules.Ledger.Infrastructure;

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
