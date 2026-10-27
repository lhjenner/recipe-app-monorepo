using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Ingredients_Id_OwnerUserId",
                table: "Ingredients",
                columns: new[] { "Id", "OwnerUserId" });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyId = table.Column<string>(type: "text", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    ServingsText = table.Column<string>(type: "text", nullable: true),
                    PrepTimeText = table.Column<string>(type: "text", nullable: true),
                    CookTimeText = table.Column<string>(type: "text", nullable: true),
                    DateAddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.UniqueConstraint("AK_Recipes_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.CheckConstraint("CK_Recipes_LegacyId_NotBlank", "\"LegacyId\" IS NULL OR \"LegacyId\" ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_Recipes_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeImages",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeImages", x => x.RecipeId);
                    table.CheckConstraint("CK_RecipeImages_Sha256", "\"Sha256\" ~ '^[0-9A-Fa-f]{64}$'");
                    table.CheckConstraint("CK_RecipeImages_StorageMetadata", "\"ByteSize\" > 0 AND \"Width\" > 0 AND \"Height\" > 0 AND \"ObjectKey\" ~ '[^[:space:]]' AND \"ContentType\" ~ '[^[:space:]]'");
                    table.ForeignKey(
                        name: "FK_RecipeImages_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeImportProvenance",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentJson = table.Column<string>(type: "jsonb", nullable: false),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MappingVersion = table.Column<string>(type: "text", nullable: false),
                    DecisionFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastAppliedRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastAppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeImportProvenance", x => x.RecipeId);
                    table.CheckConstraint("CK_RecipeImportProvenance_Hashes", "\"DocumentSha256\" ~ '^[0-9A-Fa-f]{64}$' AND \"SourceFileSha256\" ~ '^[0-9A-Fa-f]{64}$' AND \"DecisionFingerprint\" ~ '^[0-9A-Fa-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_RecipeImportProvenance_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSteps", x => x.Id);
                    table.UniqueConstraint("AK_RecipeSteps_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.CheckConstraint("CK_RecipeSteps_Position_Nonnegative", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecipeSteps_Recipes_RecipeId_OwnerUserId",
                        columns: x => new { x.RecipeId, x.OwnerUserId },
                        principalTable: "Recipes",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTags", x => x.Id);
                    table.CheckConstraint("CK_RecipeTags_Position_Nonnegative", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecipeTags_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeStepItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    QuantityText = table.Column<string>(type: "text", nullable: true),
                    UnitText = table.Column<string>(type: "text", nullable: true),
                    NotesText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeStepItems", x => x.Id);
                    table.CheckConstraint("CK_RecipeStepItems_Kind_IngredientId", "\"Kind\" IN (0, 1, 2) AND ((\"Kind\" = 0 AND \"IngredientId\" IS NOT NULL) OR (\"Kind\" IN (1, 2) AND \"IngredientId\" IS NULL))");
                    table.CheckConstraint("CK_RecipeStepItems_Position_Nonnegative", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecipeStepItems_Ingredients_IngredientId_OwnerUserId",
                        columns: x => new { x.IngredientId, x.OwnerUserId },
                        principalTable: "Ingredients",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeStepItems_RecipeSteps_RecipeStepId_OwnerUserId",
                        columns: x => new { x.RecipeStepId, x.OwnerUserId },
                        principalTable: "RecipeSteps",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OwnerUserId_LegacyId",
                table: "Recipes",
                columns: new[] { "OwnerUserId", "LegacyId" },
                unique: true,
                filter: "\"LegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OwnerUserId_NormalizedName",
                table: "Recipes",
                columns: new[] { "OwnerUserId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeStepItems_IngredientId_OwnerUserId",
                table: "RecipeStepItems",
                columns: new[] { "IngredientId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeStepItems_RecipeStepId_OwnerUserId",
                table: "RecipeStepItems",
                columns: new[] { "RecipeStepId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeStepItems_RecipeStepId_Position",
                table: "RecipeStepItems",
                columns: new[] { "RecipeStepId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId_OwnerUserId",
                table: "RecipeSteps",
                columns: new[] { "RecipeId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId_Position",
                table: "RecipeSteps",
                columns: new[] { "RecipeId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTags_RecipeId_Position",
                table: "RecipeTags",
                columns: new[] { "RecipeId", "Position" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "PreventRecipeIdentityChanges"() RETURNS trigger AS $recipe_identity$
                BEGIN
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                        OR NEW."LegacyId" IS DISTINCT FROM OLD."LegacyId"
                        OR NEW."OwnerUserId" IS DISTINCT FROM OLD."OwnerUserId" THEN
                        RAISE EXCEPTION 'Recipe identity and ownership are immutable'
                            USING ERRCODE = '23000';
                    END IF;
                    RETURN NEW;
                END;
                $recipe_identity$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_Recipes_ImmutableIdentity"
                    BEFORE UPDATE OF "Id", "LegacyId", "OwnerUserId" ON "Recipes"
                    FOR EACH ROW EXECUTE FUNCTION "PreventRecipeIdentityChanges"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeImages");

            migrationBuilder.DropTable(
                name: "RecipeImportProvenance");

            migrationBuilder.DropTable(
                name: "RecipeStepItems");

            migrationBuilder.DropTable(
                name: "RecipeTags");

            migrationBuilder.DropTable(
                name: "RecipeSteps");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.Sql("""DROP FUNCTION "PreventRecipeIdentityChanges"();""");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Ingredients_Id_OwnerUserId",
                table: "Ingredients");
        }
    }
}
