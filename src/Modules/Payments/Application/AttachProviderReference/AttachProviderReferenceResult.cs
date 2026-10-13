namespace Demopay.Modules.Payments.Application.AttachProviderReference;

public sealed record AttachProviderReferenceResult(
    Guid PaymentId,
    string Provider,
    string Reference);