using Microsoft.EntityFrameworkCore;
using Npgsql;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentProviderReferencePersistence(PaymentsDbContext context)
    : IPaymentProviderReferencePersistence
{
    public async Task<bool> AttachAsync(PaymentId paymentId, ProviderPaymentReference reference,
        CancellationToken cancellationToken = default)
    {
        reference = ProviderPaymentReference.Create(reference.Provider, reference.Reference);
        try
        {
            var updated = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE payments SET provider = {reference.Provider}, provider_payment_reference = {reference.Reference}
                WHERE id = {paymentId.Value} AND
                    ((provider IS NULL AND provider_payment_reference IS NULL) OR
                     (provider = {reference.Provider} AND provider_payment_reference = {reference.Reference}))
                """, cancellationToken);
            if (updated == 1) return true;
            if (await context.Payments.AsNoTracking().AnyAsync(x => x.Id == paymentId, cancellationToken))
                throw new ProviderPaymentReferenceConflictException();
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ux_payments_provider_payment_reference")
        {
            throw new ProviderPaymentReferenceConflictException(exception);
        }
    }
}
