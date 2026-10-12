namespace SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;

public sealed record GetPaymentByProviderReferenceResult(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string ProviderReference);