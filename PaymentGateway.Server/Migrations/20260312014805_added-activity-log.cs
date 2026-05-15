using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Server.Migrations
{
    /// <inheritdoc />
    public partial class addedactivitylog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SnapTransactions_EnvironmentId",
                schema: "payment",
                table: "SnapTransactions");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SessionToken = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnapTransactions_EnvironmentId_CallerOrderId",
                schema: "payment",
                table: "SnapTransactions",
                columns: new[] { "EnvironmentId", "CallerOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Category",
                schema: "audit",
                table: "ActivityLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Timestamp",
                schema: "audit",
                table: "ActivityLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                schema: "audit",
                table: "ActivityLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs",
                schema: "audit");

            migrationBuilder.DropIndex(
                name: "IX_SnapTransactions_EnvironmentId_CallerOrderId",
                schema: "payment",
                table: "SnapTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_SnapTransactions_EnvironmentId",
                schema: "payment",
                table: "SnapTransactions",
                column: "EnvironmentId");
        }
    }
}
