using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.CreatePayment;

public sealed class CreatePaymentHandler(
    IPaymentRepository paymentRepository)
{
    public async Task<CreatePaymentResult> HandleAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = Payment.Create(
            command.Amount,
            command.Currency);

        await paymentRepository.AddAsync(
            payment,
            cancellationToken);

        return new CreatePaymentResult(
            payment.Id.Value,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString());
    }
}