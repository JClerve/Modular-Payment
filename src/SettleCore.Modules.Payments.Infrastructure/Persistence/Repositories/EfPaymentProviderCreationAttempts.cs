using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentProviderCreationAttempts(PaymentsDbContext context, TimeProvider clock)
    : IPaymentProviderCreationAttempts
{
    public async Task<DateTimeOffset> GetOrRecordAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment_provider_creation_attempts (payment_id, started_at)
            VALUES ({paymentId.Value}, {now}) ON CONFLICT (payment_id) DO NOTHING
            """, cancellationToken);
        return await context.PaymentProviderCreationAttempts.AsNoTracking()
            .Where(x => x.PaymentId == paymentId).Select(x => x.StartedAt).SingleAsync(cancellationToken);
    }
}
