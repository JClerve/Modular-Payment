using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class PaymentLedgerPostingPreparationConfiguration : IEntityTypeConfiguration<PaymentLedgerPostingPreparation>
{
    public void Configure(EntityTypeBuilder<PaymentLedgerPostingPreparation> builder)
    {
        builder.ToTable("payment_ledger_posting_preparations");
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PaymentId).HasColumnName("payment_id")
            .HasConversion(id => id.Value, value => PaymentId.From(value)).ValueGeneratedNever();
        builder.HasOne<Payment>().WithOne().HasForeignKey<PaymentLedgerPostingPreparation>(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.LedgerId).HasColumnName("ledger_id");
        builder.Property(x => x.ProcessorReceivableAccountId).HasColumnName("processor_receivable_account_id");
        builder.Property(x => x.MerchantPayableAccountId).HasColumnName("merchant_payable_account_id");
        builder.Property(x => x.PlatformRevenueAccountId).HasColumnName("platform_revenue_account_id");
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.GrossAmountMinorUnits).HasColumnName("gross_amount_minor_units");
        builder.Property(x => x.FeeAmountMinorUnits).HasColumnName("fee_amount_minor_units");
    }
}
