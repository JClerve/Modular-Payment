using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.DesignTime;

public sealed class PaymentsDbContextFactory
    : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=settlecore_design;Username=postgres")
            .Options;

        return new PaymentsDbContext(options);
    }
}