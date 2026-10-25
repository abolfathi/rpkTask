using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt_Id_Active",
                table: "Orders",
                columns: new[] { "CreatedAt", "Id" },
                descending: new[] { true, true },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_Status_CreatedAt_Id_Active",
                table: "Orders",
                columns: new[] { "CustomerId", "Status", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedAt_Id_Active",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId_Status_CreatedAt_Id_Active",
                table: "Orders");
        }
    }
}
