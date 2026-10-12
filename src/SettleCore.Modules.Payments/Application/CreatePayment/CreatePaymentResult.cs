namespace SettleCore.Modules.Payments.Application.CreatePayment;

public sealed record CreatePaymentResult(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status);