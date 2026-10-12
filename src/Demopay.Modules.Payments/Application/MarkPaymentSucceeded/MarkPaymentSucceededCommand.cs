namespace Demopay.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed record MarkPaymentSucceededCommand(
    Guid PaymentId);