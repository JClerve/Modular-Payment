namespace SettleCore.Api.BackgroundTasks;

public static class BackgroundTaskServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentLedgerPostingWorker(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PaymentLedgerPostingWorkerOptions>()
            .Bind(configuration.GetSection("PaymentLedgerPostingWorker"))
            .Validate(static settings => settings.BatchSize > 0 &&
                settings.PollIntervalMilliseconds > 0 && settings.RetryDelaySeconds > 0,
                "Payment posting batch size, poll interval and retry delay must be positive.")
            .ValidateOnStart();
        services.AddHostedService<PaymentLedgerPostingWorker>();
        return services;
    }
}
