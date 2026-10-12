namespace SettleCore.Modules.Payments.Application.Providers;

public sealed class ProviderPaymentCorrelationException()
    : Exception("Provider success evidence does not match the stored payment and expected mode.");
