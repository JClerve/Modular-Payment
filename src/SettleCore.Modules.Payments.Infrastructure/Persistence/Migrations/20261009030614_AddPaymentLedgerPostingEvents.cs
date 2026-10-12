using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SettleCore.Modules.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentLedgerPostingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_ledger_posting_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_ledger_posting_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_ledger_posting_events_payment_ledger_posting_intent~",
                        column: x => x.payment_id,
                        principalTable: "payment_ledger_posting_intents",
                        principalColumn: "payment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_posting_events_payment_id_kind",
                table: "payment_ledger_posting_events",
                columns: ["payment_id", "kind"],
                unique: true,
                filter: "\"kind\" IN ('IntentRecorded', 'PostingAcknowledged')");

            migrationBuilder.CreateIndex(
                name: "IX_payment_ledger_posting_events_payment_id_occurred_at_id",
                table: "payment_ledger_posting_events",
                columns: ["payment_id", "occurred_at", "id"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_ledger_posting_events");
        }
    }
}
