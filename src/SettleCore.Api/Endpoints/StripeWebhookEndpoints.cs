using System.Text;
using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;

namespace SettleCore.Api.Endpoints;

public static class StripeWebhookEndpoints
{
    public static IEndpointRouteBuilder MapStripeWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/webhooks/stripe", async Task<IResult> (
            HttpRequest request, IOptions<StripePaymentOptions> options,
            IServiceProvider services, CancellationToken cancellationToken) =>
        {
            var settings = options.Value;
            if (!settings.Enabled) return Results.NotFound();

            using var reader = new StreamReader(request.Body, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            try
            {
                var decoder = services.GetRequiredService<IPaymentProviderWebhookDecoder>();
                var evidence = decoder.Decode(payload, request.Headers["Stripe-Signature"].ToString());
                if (evidence is null) return Results.Ok();
                if (evidence.IsLiveMode != settings.IsLiveMode) return Results.BadRequest();

                var inbox = services.GetRequiredService<IPaymentProviderEventInbox>();
                await inbox.ReceiveAsync(evidence, cancellationToken);
                return Results.Ok();
            }
            catch (InvalidProviderWebhookException)
            {
                return Results.BadRequest();
            }
            catch (ProviderPaymentEventConflictException)
            {
                return Results.Conflict();
            }
        })
        .WithName("ReceiveStripeWebhook")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
        return endpoints;
    }
}
