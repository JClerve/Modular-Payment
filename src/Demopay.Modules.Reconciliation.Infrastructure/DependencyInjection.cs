using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Demopay.Modules.Reconciliation.Application;
using Demopay.Modules.Reconciliation.Application.CreateReconciliation;
using Demopay.Modules.Reconciliation.Application.GetReconciliationById;
using Demopay.Modules.Reconciliation.Infrastructure.Persistence;

namespace Demopay.Modules.Reconciliation.Infrastructure;

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
