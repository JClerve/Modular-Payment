namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed class InvalidProviderWebhookException : Exception
{
    public InvalidProviderWebhookException() : base("Provider webhook authentication or success evidence is invalid.")
    {
    }
}
