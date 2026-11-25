using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerEmailPerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_customers_email",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ux_customers_owner_email",
                table: "customers",
                columns: new[] { "user_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customers_unowned_email",
                table: "customers",
                column: "email",
                unique: true,
                filter: "user_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_customers_owner_email",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ux_customers_unowned_email",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ux_customers_email",
                table: "customers",
                column: "email",
                unique: true);
        }
    }
}
