namespace SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;

public sealed record GetPaymentByProviderReferenceQuery(
    string Provider,
    string ProviderReference);