using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RecipeApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupermarketChains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupermarketChains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stores_SupermarketChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "SupermarketChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SupermarketChains",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("19f8486e-7b2c-4e10-949f-ecf0d2039b36"), "new-world", "New World" },
                    { new Guid("360fab45-a7c0-4be5-88d8-bb5a272fc2d6"), "woolworths", "Woolworths" },
                    { new Guid("a2d39069-a4d6-4837-8b0b-7f0a858d552a"), "pak-n-save", "Pak'n Save" }
                });

            migrationBuilder.InsertData(
                table: "Stores",
                columns: new[] { "Id", "ChainId", "Code", "DisplayOrder", "Name" },
                values: new object[,]
                {
                    { new Guid("3e50666e-4aac-458f-9ae8-2eacba31ec6b"), new Guid("360fab45-a7c0-4be5-88d8-bb5a272fc2d6"), "woolworths-carlyle", 3, "Woolworths Carlyle" },
                    { new Guid("67af7315-c4e5-4f9e-89b3-7c3a71ba11da"), new Guid("19f8486e-7b2c-4e10-949f-ecf0d2039b36"), "new-world-greenmeadows", 1, "New World Greenmeadows" },
                    { new Guid("b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c"), new Guid("a2d39069-a4d6-4837-8b0b-7f0a858d552a"), "pak-n-save-tamatea", 2, "Pak'n Save Tamatea" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_OwnerUserId_NormalizedName",
                table: "Categories",
                columns: new[] { "OwnerUserId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OwnerUserId_NormalizedName",
                table: "Departments",
                columns: new[] { "OwnerUserId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ChainId",
                table: "Stores",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Code",
                table: "Stores",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupermarketChains_Code",
                table: "SupermarketChains",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "SupermarketChains");
        }
    }
}
