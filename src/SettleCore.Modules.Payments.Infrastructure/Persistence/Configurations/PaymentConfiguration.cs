using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration
    : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => PaymentId.From(value))
            .ValueGeneratedNever();

        builder.Property(payment => payment.Amount)
            .HasColumnName("amount");

        builder.Property(payment => payment.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(payment => payment.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.ComplexProperty(
            payment => payment.ProviderReference,
            providerReference =>
            {
                providerReference.HasField(
                    "_providerReference");

                providerReference
                    .Property(reference => reference.Provider)
                    .HasColumnName("provider");

                providerReference
                    .Property(reference => reference.Reference)
                    .HasColumnName("provider_payment_reference");
            });
    }
}