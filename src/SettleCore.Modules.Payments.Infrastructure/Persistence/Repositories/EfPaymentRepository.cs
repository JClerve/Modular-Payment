using Microsoft.EntityFrameworkCore;
using Npgsql;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

public sealed class EfPaymentRepository(
    PaymentsDbContext dbContext)
    : IPaymentRepository
{
    public async Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        await dbContext.Payments.AddAsync(
            payment,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public Task<Payment?> GetByIdAsync(
        PaymentId id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Payments.SingleOrDefaultAsync(
            payment => payment.Id == id,
            cancellationToken);
    }

    public Task<Payment?> GetByProviderReferenceAsync(
        ProviderPaymentReference providerReference,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Payments.SingleOrDefaultAsync(
            payment =>
                payment.ProviderReference == providerReference,
            cancellationToken);
    }

    public async Task UpdateAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        dbContext.Payments.Update(payment);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName:
                    "ux_payments_provider_payment_reference"
            })
        {
            throw new ProviderPaymentReferenceConflictException(
                exception);
        }
    }
}
