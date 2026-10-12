namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed record MarkPaymentSucceededResult(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status);