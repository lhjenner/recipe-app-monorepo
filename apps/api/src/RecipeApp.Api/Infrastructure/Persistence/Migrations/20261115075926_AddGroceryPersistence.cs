using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroceryPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Departments_Id_OwnerUserId",
                table: "Departments",
                columns: new[] { "Id", "OwnerUserId" });

            migrationBuilder.CreateTable(
                name: "GroceryLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryLists", x => x.Id);
                    table.UniqueConstraint("AK_GroceryLists_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.ForeignKey(
                        name: "FK_GroceryLists_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroceryCommandReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryListId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultListVersion = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryCommandReceipts", x => x.Id);
                    table.CheckConstraint("CK_GroceryCommandReceipts_CommandId", "\"CommandId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_GroceryCommandReceipts_CommandType", "\"CommandType\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_GroceryCommandReceipts_Fingerprint", "\"RequestFingerprint\" ~ '^[0-9A-Fa-f]{64}$'");
                    table.CheckConstraint("CK_GroceryCommandReceipts_ResultJson", "jsonb_typeof(\"ResultJson\") = 'object'");
                    table.ForeignKey(
                        name: "FK_GroceryCommandReceipts_GroceryLists_GroceryListId_OwnerUser~",
                        columns: x => new { x.GroceryListId, x.OwnerUserId },
                        principalTable: "GroceryLists",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroceryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryListId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    NameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Dimension = table.Column<int>(type: "integer", nullable: true),
                    CountLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ConsolidationKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PurchaseUnit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OutstandingPurchaseQuantity = table.Column<decimal>(type: "numeric(28,9)", precision: 28, scale: 9, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryItems", x => x.Id);
                    table.UniqueConstraint("AK_GroceryItems_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.UniqueConstraint("AK_GroceryItems_Id_OwnerUserId_GroceryListId", x => new { x.Id, x.OwnerUserId, x.GroceryListId });
                    table.CheckConstraint("CK_GroceryItems_CountLabel", "(\"Dimension\" = 0 AND \"CountLabel\" IS NOT NULL AND \"CountLabel\" ~ '[^[:space:]]') OR (\"Dimension\" IS DISTINCT FROM 0 AND \"CountLabel\" IS NULL)");
                    table.CheckConstraint("CK_GroceryItems_NameAndUnit_NotBlank", "\"NormalizedName\" ~ '[^[:space:]]' AND \"PurchaseUnit\" ~ '[^[:space:]]' AND \"ConsolidationKey\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_GroceryItems_OutstandingPurchaseQuantity_Nonnegative", "\"OutstandingPurchaseQuantity\" >= 0");
                    table.CheckConstraint("CK_GroceryItems_State_Dimension", "\"State\" IN (0, 1, 2) AND (\"Dimension\" IS NULL OR \"Dimension\" IN (0, 1, 2))");
                    table.ForeignKey(
                        name: "FK_GroceryItems_Departments_DepartmentId_OwnerUserId",
                        columns: x => new { x.DepartmentId, x.OwnerUserId },
                        principalTable: "Departments",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryItems_GroceryLists_GroceryListId_OwnerUserId",
                        columns: x => new { x.GroceryListId, x.OwnerUserId },
                        principalTable: "GroceryLists",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryItems_Ingredients_IngredientId_OwnerUserId",
                        columns: x => new { x.IngredientId, x.OwnerUserId },
                        principalTable: "Ingredients",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryItems_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroceryRecipeAdditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryListId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryRecipeAdditions", x => x.Id);
                    table.UniqueConstraint("AK_GroceryRecipeAdditions_Id_Owner_List", x => new { x.Id, x.OwnerUserId, x.GroceryListId });
                    table.ForeignKey(
                        name: "FK_GroceryRecipeAdditions_GroceryLists_GroceryListId_OwnerUser~",
                        columns: x => new { x.GroceryListId, x.OwnerUserId },
                        principalTable: "GroceryLists",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryRecipeAdditions_Recipes_RecipeId_OwnerUserId",
                        columns: x => new { x.RecipeId, x.OwnerUserId },
                        principalTable: "Recipes",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroceryPurchaseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryListId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,9)", precision: 28, scale: 9, nullable: false),
                    PurchaseUnit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NoteSnapshot = table.Column<string>(type: "text", nullable: true),
                    StoreIdSnapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    ReversesEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedContributionIdsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryPurchaseEvents", x => x.Id);
                    table.UniqueConstraint("AK_GroceryPurchaseEvents_Id_Owner_List", x => new { x.Id, x.OwnerUserId, x.GroceryListId });
                    table.CheckConstraint("CK_GroceryPurchaseEvents_Kind_Reference", "\"Kind\" IN (0, 1) AND ((\"Kind\" = 0 AND \"ReversesEventId\" IS NULL) OR (\"Kind\" = 1 AND \"ReversesEventId\" IS NOT NULL))");
                    table.CheckConstraint("CK_GroceryPurchaseEvents_Origin", "\"Origin\" IN (0, 1)");
                    table.CheckConstraint("CK_GroceryPurchaseEvents_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_GroceryPurchaseEvents_Snapshot", "jsonb_typeof(\"AffectedContributionIdsJson\") = 'array'");
                    table.ForeignKey(
                        name: "FK_GroceryPurchaseEvents_GroceryItems_GroceryItemId_OwnerUserI~",
                        columns: x => new { x.GroceryItemId, x.OwnerUserId, x.GroceryListId },
                        principalTable: "GroceryItems",
                        principalColumns: new[] { "Id", "OwnerUserId", "GroceryListId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryPurchaseEvents_GroceryLists_GroceryListId_OwnerUserId",
                        columns: x => new { x.GroceryListId, x.OwnerUserId },
                        principalTable: "GroceryLists",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryPurchaseEvents_GroceryPurchaseEvents_ReversesEventId~",
                        columns: x => new { x.ReversesEventId, x.OwnerUserId, x.GroceryListId },
                        principalTable: "GroceryPurchaseEvents",
                        principalColumns: new[] { "Id", "OwnerUserId", "GroceryListId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroceryContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryListId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroceryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    RecipeAdditionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalNameText = table.Column<string>(type: "text", nullable: true),
                    OriginalAmountText = table.Column<string>(type: "text", nullable: true),
                    OriginalUnitText = table.Column<string>(type: "text", nullable: true),
                    OriginalNotesText = table.Column<string>(type: "text", nullable: true),
                    InterpretedAmount = table.Column<decimal>(type: "numeric(28,9)", precision: 28, scale: 9, nullable: true),
                    Dimension = table.Column<int>(type: "integer", nullable: true),
                    CountLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    UnresolvedReason = table.Column<string>(type: "text", nullable: true),
                    SourceRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRecipeStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRecipeStepItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRecipeNameSnapshot = table.Column<string>(type: "text", nullable: true),
                    ManualPurchaseIncrement = table.Column<decimal>(type: "numeric(28,9)", precision: 28, scale: 9, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryContributions", x => x.Id);
                    table.CheckConstraint("CK_GroceryContributions_Amounts", "(\"InterpretedAmount\" IS NULL OR \"InterpretedAmount\" > 0) AND (\"ManualPurchaseIncrement\" IS NULL OR \"ManualPurchaseIncrement\" >= 0)");
                    table.CheckConstraint("CK_GroceryContributions_CountLabel", "(\"Dimension\" = 0 AND \"CountLabel\" IS NOT NULL AND \"CountLabel\" ~ '[^[:space:]]') OR (\"Dimension\" IS DISTINCT FROM 0 AND \"CountLabel\" IS NULL)");
                    table.CheckConstraint("CK_GroceryContributions_Origin_State", "\"Origin\" IN (0, 1) AND \"State\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_GroceryContributions_Source", "(\"Origin\" = 0 AND \"RecipeAdditionId\" IS NULL AND \"SourceRecipeId\" IS NULL) OR (\"Origin\" = 1 AND \"RecipeAdditionId\" IS NOT NULL AND \"SourceRecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GroceryContributions_GroceryItems_GroceryItemId_OwnerUserId~",
                        columns: x => new { x.GroceryItemId, x.OwnerUserId, x.GroceryListId },
                        principalTable: "GroceryItems",
                        principalColumns: new[] { "Id", "OwnerUserId", "GroceryListId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryContributions_GroceryLists_GroceryListId_OwnerUserId",
                        columns: x => new { x.GroceryListId, x.OwnerUserId },
                        principalTable: "GroceryLists",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryContributions_GroceryRecipeAdditions_RecipeAdditionI~",
                        columns: x => new { x.RecipeAdditionId, x.OwnerUserId, x.GroceryListId },
                        principalTable: "GroceryRecipeAdditions",
                        principalColumns: new[] { "Id", "OwnerUserId", "GroceryListId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroceryContributions_Recipes_SourceRecipeId_OwnerUserId",
                        columns: x => new { x.SourceRecipeId, x.OwnerUserId },
                        principalTable: "Recipes",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryCommandReceipts_GroceryListId_OwnerUserId",
                table: "GroceryCommandReceipts",
                columns: new[] { "GroceryListId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryCommandReceipts_Owner_List_Command",
                table: "GroceryCommandReceipts",
                columns: new[] { "OwnerUserId", "GroceryListId", "CommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_GroceryItemId_OwnerUserId_GroceryListId",
                table: "GroceryContributions",
                columns: new[] { "GroceryItemId", "OwnerUserId", "GroceryListId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_GroceryListId_OwnerUserId",
                table: "GroceryContributions",
                columns: new[] { "GroceryListId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_Owner_List_Item_State",
                table: "GroceryContributions",
                columns: new[] { "OwnerUserId", "GroceryListId", "GroceryItemId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_Owner_RecipeAddition",
                table: "GroceryContributions",
                columns: new[] { "OwnerUserId", "RecipeAdditionId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_RecipeAdditionId_OwnerUserId_GroceryLi~",
                table: "GroceryContributions",
                columns: new[] { "RecipeAdditionId", "OwnerUserId", "GroceryListId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryContributions_SourceRecipeId_OwnerUserId",
                table: "GroceryContributions",
                columns: new[] { "SourceRecipeId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_ActiveConsolidationKey",
                table: "GroceryItems",
                columns: new[] { "OwnerUserId", "GroceryListId", "ConsolidationKey" },
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_DepartmentId_OwnerUserId",
                table: "GroceryItems",
                columns: new[] { "DepartmentId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_GroceryListId_OwnerUserId",
                table: "GroceryItems",
                columns: new[] { "GroceryListId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_IngredientId_OwnerUserId",
                table: "GroceryItems",
                columns: new[] { "IngredientId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_OwnerUserId_GroceryListId_State",
                table: "GroceryItems",
                columns: new[] { "OwnerUserId", "GroceryListId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryItems_StoreId",
                table: "GroceryItems",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryLists_OwnerUserId",
                table: "GroceryLists",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPurchaseEvents_GroceryItemId_OwnerUserId_GroceryList~",
                table: "GroceryPurchaseEvents",
                columns: new[] { "GroceryItemId", "OwnerUserId", "GroceryListId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPurchaseEvents_GroceryListId_OwnerUserId",
                table: "GroceryPurchaseEvents",
                columns: new[] { "GroceryListId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPurchaseEvents_OneReversalPerEvent",
                table: "GroceryPurchaseEvents",
                columns: new[] { "OwnerUserId", "ReversesEventId" },
                unique: true,
                filter: "\"ReversesEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPurchaseEvents_Owner_List_Date_Id",
                table: "GroceryPurchaseEvents",
                columns: new[] { "OwnerUserId", "GroceryListId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryPurchaseEvents_ReversesEventId_OwnerUserId_GroceryLi~",
                table: "GroceryPurchaseEvents",
                columns: new[] { "ReversesEventId", "OwnerUserId", "GroceryListId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryRecipeAdditions_GroceryListId_OwnerUserId",
                table: "GroceryRecipeAdditions",
                columns: new[] { "GroceryListId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryRecipeAdditions_Owner_List_Recipe",
                table: "GroceryRecipeAdditions",
                columns: new[] { "OwnerUserId", "GroceryListId", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryRecipeAdditions_RecipeId_OwnerUserId",
                table: "GroceryRecipeAdditions",
                columns: new[] { "RecipeId", "OwnerUserId" });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "PreventGroceryPurchaseEventMutation"() RETURNS trigger AS $grocery_purchase_event$
                BEGIN
                    RAISE EXCEPTION 'Grocery purchase events are append-only'
                        USING ERRCODE = '23000';
                END;
                $grocery_purchase_event$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_GroceryPurchaseEvents_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "GroceryPurchaseEvents"
                    FOR EACH ROW EXECUTE FUNCTION "PreventGroceryPurchaseEventMutation"();

                CREATE FUNCTION "ValidateGroceryPurchaseReversal"() RETURNS trigger AS $grocery_reversal$
                DECLARE purchase_event "GroceryPurchaseEvents"%ROWTYPE;
                BEGIN
                    IF NEW."Kind" = 1 THEN
                        SELECT * INTO purchase_event
                        FROM "GroceryPurchaseEvents"
                        WHERE "Id" = NEW."ReversesEventId";

                        IF NOT FOUND
                            OR purchase_event."Kind" <> 0
                            OR purchase_event."GroceryItemId" <> NEW."GroceryItemId"
                            OR purchase_event."OwnerUserId" <> NEW."OwnerUserId"
                            OR purchase_event."GroceryListId" <> NEW."GroceryListId"
                            OR purchase_event."Quantity" <> NEW."Quantity"
                            OR purchase_event."PurchaseUnit" <> NEW."PurchaseUnit"
                            OR purchase_event."Origin" <> NEW."Origin" THEN
                            RAISE EXCEPTION 'A reversal must match one same-owner purchase event'
                                USING ERRCODE = '23000';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $grocery_reversal$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_GroceryPurchaseEvents_ValidateReversal"
                    BEFORE INSERT ON "GroceryPurchaseEvents"
                    FOR EACH ROW EXECUTE FUNCTION "ValidateGroceryPurchaseReversal"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroceryCommandReceipts");

            migrationBuilder.DropTable(
                name: "GroceryContributions");

            migrationBuilder.DropTable(
                name: "GroceryPurchaseEvents");

            migrationBuilder.DropTable(
                name: "GroceryRecipeAdditions");

            migrationBuilder.DropTable(
                name: "GroceryItems");

            migrationBuilder.DropTable(
                name: "GroceryLists");

            migrationBuilder.Sql(
                """
                DROP FUNCTION "ValidateGroceryPurchaseReversal"();
                DROP FUNCTION "PreventGroceryPurchaseEventMutation"();
                """);

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Departments_Id_OwnerUserId",
                table: "Departments");
        }
    }
}
