namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentLedgerPostingPort
{
    Task PostAsync(
        PaymentLedgerPostingRequest request,
        CancellationToken cancellationToken = default);
}