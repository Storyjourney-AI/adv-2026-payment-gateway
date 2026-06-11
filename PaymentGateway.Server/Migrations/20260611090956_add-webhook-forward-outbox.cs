using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Server.Migrations
{
    /// <inheritdoc />
    public partial class addwebhookforwardoutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookForwardOutbox",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MidtransOrderId = table.Column<string>(type: "text", nullable: false),
                    CallerOrderId = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResponseCode = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookForwardOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookForwardOutbox_SnapTransactions_SnapTransactionId",
                        column: x => x.SnapTransactionId,
                        principalSchema: "payment",
                        principalTable: "SnapTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookForwardOutbox_SnapTransactionId",
                schema: "payment",
                table: "WebhookForwardOutbox",
                column: "SnapTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookForwardOutbox_Status_NextAttemptAt",
                schema: "payment",
                table: "WebhookForwardOutbox",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookForwardOutbox",
                schema: "payment");
        }
    }
}
