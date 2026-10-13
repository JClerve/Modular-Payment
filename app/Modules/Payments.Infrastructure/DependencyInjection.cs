using Demopay.Modules.Payments.Application.RecoverProviderPayment;
using Microsoft.Extensions.Options;
using Demopay.Modules.Payments.Application.GetProviderPayment;
using Demopay.Modules.Payments.Application.CreateProviderPayment;
using Demopay.Modules.Payments.Infrastructure.Integrations.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Demopay.Modules.Payments.Infrastructure.Dispatch;
using Demopay.Modules.Payments.Application.Abstractions;
using Demopay.Modules.Payments.Application.AttachProviderReference;
using Demopay.Modules.Payments.Application.CreatePayment;
using Demopay.Modules.Payments.Application.PreparePaymentLedgerPosting;
using Demopay.Modules.Payments.Application.DispatchPaymentLedgerPosting;
using Demopay.Modules.Payments.Application.GetPaymentLedgerPosting;
using Demopay.Modules.Payments.Application.GetPaymentById;
using Demopay.Modules.Payments.Application.GetPaymentByProviderReference;
using Demopay.Modules.Payments.Application.MarkPaymentSucceeded;
using Demopay.Modules.Payments.Infrastructure.Integrations.Ledger;
using Demopay.Modules.Payments.Infrastructure.Persistence;
using Demopay.Modules.Payments.Infrastructure.Persistence.Repositories;

namespace Demopay.Modules.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Payments")
            ?? throw new InvalidOperationException(
                "Connection string 'Payments' is not configured.");

        services.AddOptions<StripePaymentOptions>()
            .Bind(configuration.GetSection("Payments:Stripe"))
            .Validate(static settings => !settings.Enabled ||
                (!string.IsNullOrWhiteSpace(settings.ApiKey) &&
                 !string.IsNullOrWhiteSpace(settings.WebhookSecret) &&
                 settings.SignatureToleranceSeconds > 0 && settings.IsLiveMode.HasValue),
                "Enabled Stripe requires explicit API key, webhook secret, positive signature tolerance and live/test mode.")
            .ValidateOnStart();
        services.AddSingleton<global::Stripe.StripeClient>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<StripePaymentOptions>>().Value;
            if (!settings.Enabled) throw new InvalidOperationException("Stripe payments are disabled.");
            return new global::Stripe.StripeClient(settings.ApiKey);
        });
        services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
        services.AddSingleton<IPaymentProviderReader, StripePaymentProviderReader>();
        services.AddSingleton<IPaymentProviderWebhookDecoder>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<StripePaymentOptions>>().Value;
            if (!settings.Enabled) throw new InvalidOperationException("Stripe payments are disabled.");
            return new StripePaymentWebhookDecoder(settings.WebhookSecret!, settings.SignatureToleranceSeconds,
                provider.GetRequiredService<TimeProvider>());
        });
        services.AddScoped<IPaymentProviderEventInbox, EfPaymentProviderEventInbox>();
        services.AddScoped<IPaymentProviderReferencePersistence, EfPaymentProviderReferencePersistence>();
        services.AddScoped<IPaymentProviderEventProcessor, EfPaymentProviderEventProcessor>();
        services.AddScoped<IPaymentLedgerPostingPreparationRepository, EfPaymentLedgerPostingPreparationRepository>();

        services.AddDbContext<PaymentsDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddScoped<
            IPaymentRepository,
            EfPaymentRepository>();

        services.AddScoped<
            IPaymentLedgerPostingIntentRepository,
            EfPaymentLedgerPostingIntentRepository>();

        services.AddScoped<
            IPaymentLedgerPostingPort,
            PaymentLedgerPostingAdapter>();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddMetrics();
        services.AddSingleton<PaymentLedgerPostingMetrics>();
        services.AddSingleton<PaymentLedgerPostingBatchProcessor>();
        services.AddSingleton<PaymentProviderEventMetrics>();
        services.AddSingleton<PaymentProviderEventBatchProcessor>();
        services.AddScoped<IPaymentSuccessPersistence, EfPaymentSuccessPersistence>();
        services.AddScoped<RecordPaymentSuccessHandler>();
        services.AddScoped<DispatchPaymentLedgerPostingHandler>();

        services.AddScoped<CreatePaymentHandler>();
        services.AddScoped<IPaymentProviderCreationAttempts, EfPaymentProviderCreationAttempts>();
        // Stripe may prune v1 idempotency keys after 24h; stop automatic creation retries earlier.
        services.AddSingleton(new PaymentProviderCreationRetryPolicy(TimeSpan.FromHours(23)));
        services.AddScoped<CreateProviderPaymentHandler>();
        services.AddScoped<GetProviderPaymentHandler>();
        services.AddScoped<RecoverProviderPaymentHandler>();
        services.AddScoped<PreparePaymentLedgerPostingHandler>();
        services.AddScoped<GetPaymentByIdHandler>();
        services.AddScoped<GetPaymentLedgerPostingHandler>();
        services.AddScoped<MarkPaymentSucceededHandler>();
        services.AddScoped<AttachProviderReferenceHandler>();
        services.AddScoped<GetPaymentByProviderReferenceHandler>();

        return services;
    }
}
