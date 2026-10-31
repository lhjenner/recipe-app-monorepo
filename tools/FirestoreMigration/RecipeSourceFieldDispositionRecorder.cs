using System.Text.Json;

namespace FirestoreMigration;

public static class RecipeSourceFieldDispositionRecorder
{
    private static readonly IReadOnlyDictionary<string, string> RecipeFields =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = "Recipe.LegacyId",
            ["title"] = "Recipe.Name",
            ["source"] = "Recipe.SourceText",
            ["link"] = "Recipe.SourceUrl",
            ["servings"] = "Recipe.ServingsText",
            ["prepTime"] = "Recipe.PrepTimeText",
            ["cookTime"] = "Recipe.CookTimeText",
            ["tags"] = "Recipe.Tags",
            ["steps"] = "Recipe.Steps",
            ["archived"] = "Recipe.IsArchived",
            ["dateAdded"] = "Recipe.DateAddedAtUtc",
            ["userId"] = "retained-in-provenance; legacy-owner-selection-only",
            ["imageUrl"] = "retained-in-provenance; image-manifest-validation"
        };

    public static void RecordRecipeFields(JsonElement element, string path, string legacyId,
        RecipeMigrationSource source) =>
        Record(element, path, legacyId, source, RecipeFields);

    public static void RecordStepFields(JsonElement element, string path, string legacyId,
        RecipeMigrationSource source) =>
        Record(element, path, legacyId, source, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["instructions"] = "RecipeStep.Instructions",
            ["ingredients"] = "RecipeStep.Items"
        });

    public static void RecordItemFields(JsonElement element, string path, string legacyId,
        RecipeMigrationSource source, RecipeSourceItemKind? kind)
    {
        var mappings = kind is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = "source-item-identity",
                ["name"] = "source-item-text",
                ["quantity"] = "source-item-quantity",
                ["unit"] = "source-item-unit",
                ["notes"] = "source-item-notes"
            }
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = kind == RecipeSourceItemKind.Ingredient
                    ? "Ingredient.SourceId"
                    : $"{kind}.SourceId",
                ["name"] = $"{kind}.Text",
                ["quantity"] = $"{kind}.QuantityText",
                ["unit"] = $"{kind}.UnitText",
                ["notes"] = $"{kind}.NotesText"
            };
        Record(element, path, legacyId, source, mappings,
            kind is null ? "blank-placeholder-omitted" : null);
    }

    public static void RecordDateFields(JsonElement element, string path, string legacyId,
        RecipeMigrationSource source)
    {
        var mappings = element.TryGetProperty("_seconds", out _)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["_seconds"] = "Recipe.DateAddedAtUtc",
                ["_nanoseconds"] = "Recipe.DateAddedAtUtc"
            }
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["type"] = "retained-in-provenance",
                ["seconds"] = "Recipe.DateAddedAtUtc",
                ["nanoseconds"] = "Recipe.DateAddedAtUtc"
            };
        Record(element, path, legacyId, source, mappings);
    }

    private static void Record(JsonElement element, string path, string legacyId,
        RecipeMigrationSource source, IReadOnlyDictionary<string, string> knownFields,
        string? knownDispositionForBlank = null)
    {
        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = $"{path}.{property.Name}";
            if (knownFields.TryGetValue(property.Name, out var disposition))
            {
                source.FieldDispositions.Add(new(legacyId, propertyPath,
                    knownDispositionForBlank ?? disposition));
            }
            else
            {
                source.UnknownFieldPaths.Add(propertyPath);
                source.FieldDispositions.Add(new(legacyId, propertyPath, "retained-in-provenance"));
            }
        }
    }
}
