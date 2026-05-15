using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Server.Migrations
{
    /// <inheritdoc />
    public partial class addsnaptransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SnapTransactions",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MidtransOrderId = table.Column<string>(type: "text", nullable: false),
                    CallerOrderId = table.Column<string>(type: "text", nullable: false),
                    GrossAmount = table.Column<int>(type: "integer", nullable: false),
                    MidtransEnv = table.Column<string>(type: "text", nullable: false),
                    TransactionStatus = table.Column<string>(type: "text", nullable: true),
                    MidtransTransactionId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnapTransactions_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalSchema: "payment",
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnapTransactions_EnvironmentId",
                schema: "payment",
                table: "SnapTransactions",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SnapTransactions_MidtransOrderId",
                schema: "payment",
                table: "SnapTransactions",
                column: "MidtransOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SnapTransactions",
                schema: "payment");
        }
    }
}
