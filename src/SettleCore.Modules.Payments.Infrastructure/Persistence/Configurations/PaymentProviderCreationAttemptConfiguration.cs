using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class PaymentProviderCreationAttemptConfiguration : IEntityTypeConfiguration<PaymentProviderCreationAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentProviderCreationAttempt> builder)
    {
        builder.ToTable("payment_provider_creation_attempts");
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PaymentId).HasColumnName("payment_id")
            .HasConversion(id => id.Value, value => PaymentId.From(value)).ValueGeneratedNever();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.HasOne<Payment>().WithOne().HasForeignKey<PaymentProviderCreationAttempt>(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
