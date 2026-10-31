using System.Text.Json;

namespace FirestoreMigration;

public static class RecipeSourceParser
{
    private static readonly string[] RecipeFields =
    [
        "id", "title", "source", "link", "servings", "prepTime", "cookTime", "tags", "steps",
        "archived", "dateAdded", "userId", "imageUrl"
    ];

    public static RecipeMigrationSource Parse(string recipes, string decisions)
    {
        using var recipeDocument = JsonInput.Read(recipes, JsonValueKind.Array, "recipes.json");
        using var decisionDocument = JsonInput.Read(decisions, JsonValueKind.Object, "decisions");
        var source = new RecipeMigrationSource
        {
            SourceFingerprint = RecipeSourceFingerprint.Create(recipes),
            DecisionFingerprint = RecipeSourceFingerprint.Create(decisions)
        };
        var classifications = ReadDecisions(decisionDocument.RootElement);
        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        var presentOwners = 0;
        var missingOwners = 0;

        var recipeIndex = 0;
        foreach (var element in recipeDocument.RootElement.EnumerateArray())
        {
            var path = $"recipes[{recipeIndex}]";
            JsonInput.Require(element, JsonValueKind.Object, path);
            var legacyId = Text(element, "id", path, required: true);
            if (!recipeIds.Add(legacyId))
                throw new FormatException($"{path}.id: duplicate source Recipe id '{legacyId}'.");

            var name = Text(element, "title", path, required: true);
            var tags = ReadTags(element, path);
            var steps = ReadSteps(element, path, recipeIndex, classifications, source);
            var archived = ReadArchive(element, path, source);
            var dateAdded = ReadDate(element, path, source);
            var legacyOwnerId = Text(element, "userId", path);
            if (string.IsNullOrEmpty(legacyOwnerId))
            {
                missingOwners++;
            }
            else
            {
                presentOwners++;
                source.LegacyOwnerIds.Add(legacyOwnerId);
            }

            AddUnknownFields(element, path, source, RecipeFields);
            source.Recipes.Add(new(
                legacyId,
                name,
                Text(element, "source", path),
                Text(element, "link", path),
                Text(element, "servings", path),
                Text(element, "prepTime", path),
                Text(element, "cookTime", path),
                tags,
                steps,
                archived,
                dateAdded,
                string.IsNullOrEmpty(legacyOwnerId) ? null : legacyOwnerId,
                Text(element, "imageUrl", path),
                element.GetRawText()));
            recipeIndex++;
        }

        source.OwnerFieldCounts = new(presentOwners, missingOwners);
        foreach (var unknownPath in source.UnknownFieldPaths)
            source.Warnings.Add($"{unknownPath}: unrecognized source field retained in provenance.");
        if (source.LegacyOwnerIds.Count > 1)
            source.Warnings.Add($"Multiple legacy owner IDs occur in the selected export ({source.LegacyOwnerIds.Count}).");
        return source;
    }

    private static Dictionary<Guid, ClassificationDecision> ReadDecisions(JsonElement root)
    {
        var decisions = new Dictionary<Guid, ClassificationDecision>();
        foreach (var property in root.EnumerateObject())
        {
            if (!Guid.TryParse(property.Name, out var id) || id == Guid.Empty)
                throw new FormatException($"Decision key '{property.Name}' must be a non-empty GUID.");
            JsonInput.Require(property.Value, JsonValueKind.Object, $"decision {id}");
            var name = Text(property.Value, "name", $"decision {id}", required: true);
            var classification = Text(property.Value, "classification", $"decision {id}", required: true);
            if (classification is not ("Ingredient" or "Heading" or "Note"))
                throw new FormatException($"Decision {id}: unsupported classification '{classification}'.");
            if (!decisions.TryAdd(id, new(name, classification)))
                throw new FormatException($"Duplicate decision GUID {id}.");
        }

        return decisions;
    }

    private static IReadOnlyList<string> ReadTags(JsonElement recipe, string path)
    {
        if (!recipe.TryGetProperty("tags", out var tags))
            throw new FormatException($"{path}.tags: required array is missing.");
        JsonInput.Require(tags, JsonValueKind.Array, $"{path}.tags");
        var result = new List<string>();
        var position = 0;
        foreach (var tag in tags.EnumerateArray())
        {
            JsonInput.Require(tag, JsonValueKind.String, $"{path}.tags[{position}]");
            result.Add(tag.GetString()!);
            position++;
        }
        return result;
    }

    private static IReadOnlyList<RecipeSourceStep> ReadSteps(JsonElement recipe, string recipePath,
        int recipeIndex, IReadOnlyDictionary<Guid, ClassificationDecision> decisions,
        RecipeMigrationSource source)
    {
        if (!recipe.TryGetProperty("steps", out var steps))
            throw new FormatException($"{recipePath}.steps: required array is missing.");
        JsonInput.Require(steps, JsonValueKind.Array, $"{recipePath}.steps");
        var result = new List<RecipeSourceStep>();
        var stepPosition = 0;
        foreach (var step in steps.EnumerateArray())
        {
            var stepPath = $"{recipePath}.steps[{stepPosition}]";
            JsonInput.Require(step, JsonValueKind.Object, stepPath);
            var instructions = Text(step, "instructions", stepPath);
            if (!step.TryGetProperty("ingredients", out var ingredients))
                throw new FormatException($"{stepPath}.ingredients: required array is missing.");
            JsonInput.Require(ingredients, JsonValueKind.Array, $"{stepPath}.ingredients");
            var items = ReadItems(ingredients, recipeIndex, stepPosition, decisions, source);
            AddUnknownFields(step, stepPath, source, "instructions", "ingredients");
            result.Add(new(stepPosition, instructions, items));
            stepPosition++;
        }
        return result;
    }

    private static IReadOnlyList<RecipeSourceItem> ReadItems(JsonElement ingredients, int recipeIndex,
        int stepPosition, IReadOnlyDictionary<Guid, ClassificationDecision> decisions,
        RecipeMigrationSource source)
    {
        var result = new List<RecipeSourceItem>();
        var itemPosition = 0;
        foreach (var line in ingredients.EnumerateArray())
        {
            var path = $"recipes[{recipeIndex}].steps[{stepPosition}].ingredients[{itemPosition}]";
            JsonInput.Require(line, JsonValueKind.Object, path);
            if (IsBlankPlaceholder(line))
            {
                source.BlankPlaceholders.Add(new(path, stepPosition, itemPosition, line.GetRawText()));
            }
            else
            {
                var idText = Text(line, "id", path, required: true);
                if (!Guid.TryParse(idText, out var id) || id == Guid.Empty)
                    throw new FormatException($"{path}.id: a nonblank item must have a non-empty GUID.");
                var kind = decisions.TryGetValue(id, out var decision)
                    ? decision.Classification switch
                    {
                        "Heading" => RecipeSourceItemKind.Heading,
                        "Note" => RecipeSourceItemKind.Note,
                        _ => RecipeSourceItemKind.Ingredient
                    }
                    : RecipeSourceItemKind.Ingredient;
                var text = Text(line, "name", path);
                if (kind is RecipeSourceItemKind.Heading or RecipeSourceItemKind.Note &&
                    string.IsNullOrWhiteSpace(text))
                    source.BlockingDiagnostics.Add($"{path}.name: structural text is unavailable.");
                result.Add(new(itemPosition, kind,
                    kind == RecipeSourceItemKind.Ingredient ? id : null,
                    text,
                    Text(line, "quantity", path),
                    Text(line, "unit", path),
                    Text(line, "notes", path),
                    line.GetRawText()));
            }
            AddUnknownFields(line, path, source, "id", "name", "quantity", "unit", "notes");
            itemPosition++;
        }
        return result;
    }

    private static bool IsBlankPlaceholder(JsonElement line) =>
        line.EnumerateObject().All(property =>
            property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length == 0);

    private static bool ReadArchive(JsonElement recipe, string path, RecipeMigrationSource source)
    {
        if (!recipe.TryGetProperty("archived", out var archived))
        {
            source.MissingArchiveCount++;
            source.Warnings.Add($"{path}.archived: missing; defaulted to active.");
            return false;
        }
        if (archived.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new FormatException($"{path}.archived: expected boolean.");
        return archived.GetBoolean();
    }

    private static DateTime? ReadDate(JsonElement recipe, string path, RecipeMigrationSource source)
    {
        if (!recipe.TryGetProperty("dateAdded", out var dateAdded) || dateAdded.ValueKind == JsonValueKind.Null)
        {
            source.MissingDateCount++;
            return null;
        }
        JsonInput.Require(dateAdded, JsonValueKind.Object, $"{path}.dateAdded");
        var fields = dateAdded.TryGetProperty("_seconds", out _)
            ? new[] { "_seconds", "_nanoseconds" }
            : ["type", "seconds", "nanoseconds"];
        AddUnknownFields(dateAdded, $"{path}.dateAdded", source, fields);
        return RecipeTimestampParser.Parse(dateAdded, $"{path}.dateAdded");
    }

    private static string Text(JsonElement element, string key, string path, bool required = false)
    {
        if (!element.TryGetProperty(key, out var value))
        {
            if (required) throw new FormatException($"{path}.{key}: required field is missing.");
            return "";
        }
        JsonInput.Require(value, JsonValueKind.String, $"{path}.{key}");
        var text = value.GetString()!;
        if (required && string.IsNullOrWhiteSpace(text))
            throw new FormatException($"{path}.{key}: value must not be blank.");
        return text;
    }

    private static void AddUnknownFields(JsonElement element, string path, RecipeMigrationSource source,
        params string[] knownFields)
    {
        foreach (var property in element.EnumerateObject())
            if (!knownFields.Contains(property.Name, StringComparer.Ordinal))
                source.UnknownFieldPaths.Add($"{path}.{property.Name}");
    }

}
