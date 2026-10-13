using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Abstractions;

// Provider observations do not authorize local payment completion; verified webhooks do.
public sealed record CreateProviderPaymentResult(
    ProviderPaymentReference ProviderReference,
    ProviderPaymentStatus Status,
    string? ClientSecret);
