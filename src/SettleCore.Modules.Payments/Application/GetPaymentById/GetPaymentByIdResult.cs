namespace SettleCore.Modules.Payments.Application.GetPaymentById;

public sealed record GetPaymentByIdResult(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status,
    string? Provider,
    string? ProviderReference);
