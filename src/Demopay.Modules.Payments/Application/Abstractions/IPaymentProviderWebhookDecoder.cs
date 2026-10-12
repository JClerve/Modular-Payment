namespace Demopay.Modules.Payments.Application.Abstractions;

public interface IPaymentProviderWebhookDecoder
{
    ProviderPaymentSucceededEvent? Decode(string payload, string signatureHeader);
}
