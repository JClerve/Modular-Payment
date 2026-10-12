using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed class MarkPaymentSucceededHandler(
    IPaymentRepository repository)
{
    public async Task<MarkPaymentSucceededResult?> HandleAsync(
        MarkPaymentSucceededCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var paymentId =
            PaymentId.From(command.PaymentId);

        var payment = await repository.GetByIdAsync(
            paymentId,
            cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.ProviderReference is not null)
        {
            throw new InvalidOperationException("Provider-linked payments require verified provider success evidence.");
        }

        payment.MarkSucceeded();

        await repository.UpdateAsync(
            payment,
            cancellationToken);

        return new MarkPaymentSucceededResult(
            payment.Id.Value,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString());
    }
}