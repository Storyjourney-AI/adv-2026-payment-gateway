using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentGateway.Server.Migrations
{
    /// <inheritdoc />
    public partial class addissandboxtoenvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSandbox",
                schema: "payment",
                table: "Environments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSandbox",
                schema: "payment",
                table: "Environments");
        }
    }
}
