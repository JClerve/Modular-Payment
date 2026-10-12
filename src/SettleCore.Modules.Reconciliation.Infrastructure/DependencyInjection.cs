using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SettleCore.Modules.Reconciliation.Application;
using SettleCore.Modules.Reconciliation.Application.CreateReconciliation;
using SettleCore.Modules.Reconciliation.Application.GetReconciliationById;
using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;

namespace SettleCore.Modules.Reconciliation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReconciliationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Reconciliation")
            ?? throw new InvalidOperationException(
                "Connection string 'Reconciliation' is not configured.");

        services.AddDbContext<ReconciliationDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddScoped<
            IReconciliationRepository,
            EfReconciliationRepository>();

        services.AddScoped<CreateReconciliationHandler>();
        services.AddScoped<GetReconciliationByIdHandler>();

        return services;
    }
}
