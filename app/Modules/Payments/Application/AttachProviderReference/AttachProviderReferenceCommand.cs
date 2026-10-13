namespace Demopay.Modules.Payments.Application.AttachProviderReference;

public sealed record AttachProviderReferenceCommand(
    Guid PaymentId,
    string Provider,
    string Reference);