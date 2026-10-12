using SettleCore.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SettleCore.Api.Health;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using SettleCore.Api.Endpoints;
using SettleCore.Api.BackgroundTasks;
using SettleCore.Modules.Ledger.Infrastructure;
using SettleCore.Modules.Payments.Infrastructure;
using SettleCore.Modules.Reconciliation.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddLocalApiAccess(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks()
    .AddCheck<ModuleDatabaseHealthCheck<PaymentsDbContext>>("payments-database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<ModuleDatabaseHealthCheck<LedgerDbContext>>("ledger-database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<ModuleDatabaseHealthCheck<ReconciliationDbContext>>("reconciliation-database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddPaymentLedgerPostingWorker(builder.Configuration);

builder.Services.AddOptions<PaymentProviderEventWorkerOptions>()
    .Bind(builder.Configuration.GetSection("PaymentProviderEventWorker"))
    .Validate(static settings => settings.BatchSize > 0 &&
        settings.PollIntervalMilliseconds > 0 && settings.RetryDelaySeconds > 0,
        "Provider event batch size, poll interval and retry delay must be positive.")
    .Validate<IOptions<StripePaymentOptions>>(static (settings, stripe) =>
        !settings.Enabled || (stripe.Value.Enabled && stripe.Value.IsLiveMode.HasValue),
        "Provider event worker requires enabled Stripe with explicit live/test mode.")
    .ValidateOnStart();
builder.Services.AddHostedService<PaymentProviderEventWorker>();

builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddReconciliationModule(builder.Configuration);
builder.Services.AddLedgerModule(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

var operatorEndpoints = app.MapGroup("").RequireAuthorization(LocalApiAccess.Policy);
operatorEndpoints.MapPaymentsEndpoints();
operatorEndpoints.MapReconciliationEndpoints();
operatorEndpoints.MapLedgerEndpoints();
app.MapStripeWebhookEndpoints();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = healthCheck =>
            healthCheck.Tags.Contains("ready")
    });

app.Run();

public partial class Program
{
}
