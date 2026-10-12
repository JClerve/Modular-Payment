using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class PaymentLedgerPostingIntentConfiguration
    : IEntityTypeConfiguration<PaymentLedgerPostingIntent>
{
    public void Configure(
        EntityTypeBuilder<PaymentLedgerPostingIntent> builder)
    {
        builder.ToTable("payment_ledger_posting_intents");

        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PostedAt).HasColumnName("posted_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.PaymentId })
            .HasDatabaseName("ix_payment_ledger_posting_intents_due");

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasConversion(
                id => id.Value,
                value => PaymentId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TransactionId)
            .HasColumnName("transaction_id");

        builder.Property(x => x.LedgerId)
            .HasColumnName("ledger_id");

        builder.Property(x => x.ProcessorReceivableAccountId)
            .HasColumnName("processor_receivable_account_id");

        builder.Property(x => x.MerchantPayableAccountId)
            .HasColumnName("merchant_payable_account_id");

        builder.Property(x => x.PlatformRevenueAccountId)
            .HasColumnName("platform_revenue_account_id");

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.GrossAmountMinorUnits)
            .HasColumnName("gross_amount_minor_units");

        builder.Property(x => x.FeeAmountMinorUnits)
            .HasColumnName("fee_amount_minor_units");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
    }
}