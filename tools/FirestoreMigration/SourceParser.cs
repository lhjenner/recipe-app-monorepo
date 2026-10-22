using System.Text.Json;

namespace FirestoreMigration;

public static class SourceParser
{
    public static MigrationSource Parse(string ingredients, string meta, string decisions)
    {
        var source = new MigrationSource();
        using var ingredientDocument = JsonInput.Read(ingredients, JsonValueKind.Array, "ingredients.json");
        using var metaDocument = JsonInput.Read(meta, JsonValueKind.Array, "meta.json");
        using var decisionDocument = JsonInput.Read(decisions, JsonValueKind.Object, "decisions");
        ReadDecisions(decisionDocument.RootElement, source);
        ReadMeta(metaDocument.RootElement, source);
        foreach (var element in ingredientDocument.RootElement.EnumerateArray())
        {
            JsonInput.Require(element, JsonValueKind.Object, "ingredient");
            if (!Guid.TryParse(JsonInput.Text(element, "id", true), out var id) || id == Guid.Empty)
                throw new FormatException("Ingredient id must be a non-empty GUID; no replacement is generated.");
            if (!source.SourceIds.Add(id)) source.Errors.Add($"Duplicate ingredient id {id}.");
            var name = JsonInput.Text(element, "name", true, 200);
            JsonInput.Ignored(element, "ingredients", source, "id", "name", "category", "invalid",
                "departments", "productUrls", "preferredVariant", "nutrition");
            var invalid = JsonInput.Flag(element, "invalid");
            source.Decisions.TryGetValue(id, out var decision);
            if (!invalid && decision is not null)
                source.Errors.Add($"{id}: a decision exists for a currently non-invalid ingredient.");
            if (invalid && decision is null)
            {
                source.Unclassified.Add($"{id}: {JsonSerializer.Serialize(name)}" +
                    (name.EndsWith(':') ? " (suggestion only: Heading)" : ""));
                continue;
            }
            if (invalid && decision!.Classification != "Ingredient")
            {
                if (decision.Classification == "Heading") source.Headings++;
                else source.Notes++;
                continue;
            }
            var category = JsonInput.OptionalText(element, "category", 100);
            if (category is not null && MigrationNames.Normalize(category).Length == 0) category = null;
            source.Ingredients.Add(new(id, name, category, invalid,
                ReadMappings(element, source), ProductParser.Read(element, name, source),
                NutritionParser.Read(element, name, source)));
        }
        foreach (var id in source.Decisions.Keys.Except(source.SourceIds))
            source.Warnings.Add($"Retained decision {id} is absent from this backup.");
        return source;
    }

    private static void ReadDecisions(JsonElement root, MigrationSource source)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!Guid.TryParse(property.Name, out var id) || id == Guid.Empty)
                throw new FormatException($"Decision key '{property.Name}' must be a non-empty GUID.");
            JsonInput.Require(property.Value, JsonValueKind.Object, "decision");
            var classification = JsonInput.Text(property.Value, "classification", true);
            if (classification is not ("Ingredient" or "Heading" or "Note"))
                throw new FormatException($"Decision {id}: unsupported classification '{classification}'.");
            if (!source.Decisions.TryAdd(id, new(JsonInput.Text(property.Value, "name", true), classification)))
                throw new FormatException($"Duplicate decision GUID {id}.");
            JsonInput.Ignored(property.Value, "decisions", source, "name", "classification");
        }
    }

    private static void ReadMeta(JsonElement root, MigrationSource source)
    {
        var ids = new HashSet<string>();
        foreach (var document in root.EnumerateArray())
        {
            JsonInput.Require(document, JsonValueKind.Object, "meta document");
            var id = JsonInput.Text(document, "id", true);
            if (!ids.Add(id)) throw new FormatException($"Duplicate meta document '{id}'.");
            if (id is "categories" or "departments")
            {
                if (!document.TryGetProperty("list", out var list)) throw new FormatException($"Missing {id}.list.");
                JsonInput.Require(list, JsonValueKind.Array, $"{id}.list");
                foreach (var entry in list.EnumerateArray())
                {
                    JsonInput.Require(entry, JsonValueKind.String, $"{id}.list entry");
                    var value = MigrationNames.Clean(entry.GetString()!);
                    if (value.Length is 0 or > 100)
                        throw new FormatException($"{id}.list entries must contain 1-100 characters.");
                    (id == "categories" ? source.Categories : source.Departments).Add(value);
                }
                JsonInput.Ignored(document, $"meta.{id}", source, "id", "list");
            }
            else if (id == "supermarkets")
            {
                if (!document.TryGetProperty("supermarkets", out var stores))
                    throw new FormatException("Missing supermarkets.supermarkets.");
                JsonInput.Require(stores, JsonValueKind.Array, "supermarkets");
                foreach (var store in stores.EnumerateArray())
                {
                    JsonInput.Require(store, JsonValueKind.Object, "supermarket");
                    var code = JsonInput.Text(store, "id", true);
                    if (source.StoreCodes.Contains(code))
                        throw new FormatException($"Duplicate supermarket code '{code}'.");
                    source.StoreCodes.Add(code);
                    JsonInput.Ignored(store, "meta.supermarkets.store", source, "id");
                }
                JsonInput.Ignored(document, "meta.supermarkets", source, "id", "supermarkets");
            }
            else
            {
                source.Ignored[$"meta.document.{id}"] = 1;
            }
        }
        foreach (var required in new[] { "categories", "departments", "supermarkets" })
            if (!ids.Contains(required)) throw new FormatException($"Missing meta document '{required}'.");
    }

    private static List<MappingValue> ReadMappings(JsonElement ingredient, MigrationSource source)
    {
        var result = new List<MappingValue>();
        if (!ingredient.TryGetProperty("departments", out var departments)) return result;
        JsonInput.Require(departments, JsonValueKind.Object, "departments");
        foreach (var property in departments.EnumerateObject())
        {
            JsonInput.Require(property.Value, JsonValueKind.String, $"departments.{property.Name}");
            var value = MigrationNames.Clean(property.Value.GetString()!);
            if (value.Length > 100) throw new FormatException($"Department '{property.Name}' exceeds 100 characters.");
            // Retain even empty keys for seeded-code validation; empty values create no mapping.
            source.StoreCodes.Add(property.Name);
            if (value.Length == 0) continue;
            var notStocked = string.Equals(value, "n/a", StringComparison.OrdinalIgnoreCase);
            result.Add(new(property.Name, notStocked ? null : value, notStocked));
        }
        return result;
    }
}
