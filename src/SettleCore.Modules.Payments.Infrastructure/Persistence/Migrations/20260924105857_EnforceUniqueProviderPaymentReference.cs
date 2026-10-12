using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueProviderPaymentReference : Migration
    {
        private static readonly string[] ProviderReferenceColumns =
        [
            "provider",
            "provider_payment_reference"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_payments_provider_payment_reference",
                table: "payments",
                columns: ProviderReferenceColumns,
                unique: true,
                filter:
                "provider IS NOT NULL AND " +
                "provider_payment_reference IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payments_provider_payment_reference",
                table: "payments");
        }
    }
}
