using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExcludeFromGroceryList = table.Column<bool>(type: "boolean", nullable: false),
                    EnergyKcal = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    ProteinG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    CarbohydrateG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    SugarG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    FatG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    SaturatedFatG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    FibreG = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    SodiumMg = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    NutritionSource = table.Column<string>(type: "text", nullable: true),
                    NutritionUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingredients_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientStoreMappings",
                columns: table => new
                {
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsNotStocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientStoreMappings", x => new { x.IngredientId, x.StoreId });
                    table.CheckConstraint("CK_IngredientStoreMappings_DepartmentOrNotStocked", "(\"DepartmentId\" IS NOT NULL AND NOT \"IsNotStocked\") OR (\"DepartmentId\" IS NULL AND \"IsNotStocked\")");
                    table.ForeignKey(
                        name: "FK_IngredientStoreMappings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientStoreMappings_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientStoreMappings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    VariantLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupportsBothPricingMethods = table.Column<bool>(type: "boolean", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductLinks_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductLinks_SupermarketChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "SupermarketChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CategoryId",
                table: "Ingredients",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_OwnerUserId_ArchivedAtUtc_NormalizedName",
                table: "Ingredients",
                columns: new[] { "OwnerUserId", "ArchivedAtUtc", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_OwnerUserId_NormalizedName",
                table: "Ingredients",
                columns: new[] { "OwnerUserId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientStoreMappings_DepartmentId",
                table: "IngredientStoreMappings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientStoreMappings_StoreId",
                table: "IngredientStoreMappings",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductLinks_ChainId",
                table: "ProductLinks",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductLinks_IngredientId_ChainId_Position",
                table: "ProductLinks",
                columns: new[] { "IngredientId", "ChainId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductLinks_IngredientId_ChainId_Preferred",
                table: "ProductLinks",
                columns: new[] { "IngredientId", "ChainId" },
                unique: true,
                filter: "\"IsPreferred\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientStoreMappings");

            migrationBuilder.DropTable(
                name: "ProductLinks");

            migrationBuilder.DropTable(
                name: "Ingredients");
        }
    }
}
