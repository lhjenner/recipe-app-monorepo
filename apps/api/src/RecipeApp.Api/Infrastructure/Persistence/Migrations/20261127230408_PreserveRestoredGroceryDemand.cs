using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveRestoredGroceryDemand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RestoredPurchaseQuantity",
                table: "GroceryItems",
                type: "numeric(28,9)",
                precision: 28,
                scale: 9,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestoredRecipeAssociation",
                table: "GroceryContributions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroceryItems_RestoredPurchaseQuantity",
                table: "GroceryItems",
                sql: "\"RestoredPurchaseQuantity\" >= 0 AND \"RestoredPurchaseQuantity\" <= \"OutstandingPurchaseQuantity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GroceryItems_RestoredPurchaseQuantity",
                table: "GroceryItems");

            migrationBuilder.DropColumn(
                name: "RestoredPurchaseQuantity",
                table: "GroceryItems");

            migrationBuilder.DropColumn(
                name: "IsRestoredRecipeAssociation",
                table: "GroceryContributions");
        }
    }
}
