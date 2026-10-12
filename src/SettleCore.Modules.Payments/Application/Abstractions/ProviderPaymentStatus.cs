namespace SettleCore.Modules.Payments.Application.Abstractions;

public enum ProviderPaymentStatus
{
    Pending,
    RequiresAction,
    Processing,
    RequiresCapture,
    Succeeded,
    Failed,
    Canceled
}
