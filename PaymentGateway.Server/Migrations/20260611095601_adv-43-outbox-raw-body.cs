using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Server.Migrations
{
    /// <inheritdoc />
    public partial class adv43outboxrawbody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawNotificationBody",
                schema: "payment",
                table: "WebhookForwardOutbox",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "payment",
                table: "WebhookForwardOutbox",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawNotificationBody",
                schema: "payment",
                table: "WebhookForwardOutbox");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "payment",
                table: "WebhookForwardOutbox");
        }
    }
}
