using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options)
    : DbContext(options)
{
    public DbSet<PaymentProviderCreationAttempt> PaymentProviderCreationAttempts => Set<PaymentProviderCreationAttempt>();

    public DbSet<PaymentLedgerPostingPreparation> PaymentLedgerPostingPreparations => Set<PaymentLedgerPostingPreparation>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentLedgerPostingIntent> PaymentLedgerPostingIntents =>
        Set<PaymentLedgerPostingIntent>();

    public DbSet<PaymentLedgerPostingEvent> PaymentLedgerPostingEvents =>
        Set<PaymentLedgerPostingEvent>();

    public DbSet<PaymentProviderEventReceipt> PaymentProviderEventReceipts => Set<PaymentProviderEventReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new PaymentProviderCreationAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentLedgerPostingPreparationConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentProviderEventReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentLedgerPostingEventConfiguration());

        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(
            new PaymentLedgerPostingIntentConfiguration());
    }
}
