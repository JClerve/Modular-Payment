using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentLedgerPostingIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_ledger_posting_intents",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processor_receivable_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_payable_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_revenue_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    gross_amount_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    fee_amount_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_ledger_posting_intents", x => x.payment_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_ledger_posting_intents");
        }
    }
}
