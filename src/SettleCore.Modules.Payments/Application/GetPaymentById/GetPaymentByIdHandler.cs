using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.GetPaymentById;

public sealed class GetPaymentByIdHandler(
    IPaymentRepository repository)
{
    public async Task<GetPaymentByIdResult?> HandleAsync(
        GetPaymentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var paymentId =
            PaymentId.From(query.PaymentId);

        var payment = await repository.GetByIdAsync(
            paymentId,
            cancellationToken);

        if (payment is null)
        {
            return null;
        }

        return new GetPaymentByIdResult(
            payment.Id.Value,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.ProviderReference?.Provider,
            payment.ProviderReference?.Reference);
    }
}
