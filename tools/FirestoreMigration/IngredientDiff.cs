using System.Text.Json;

namespace FirestoreMigration;

public sealed record FieldDiff(string Field, string? Before, string? Incoming);

public static class IngredientDiff
{
    public static IReadOnlyList<FieldDiff> Compare(IngredientValue before, IngredientValue incoming)
    {
        var oldFields = Fields(before);
        var newFields = Fields(incoming);
        return oldFields.Keys.Union(newFields.Keys).Order(StringComparer.Ordinal)
            .Where(key => oldFields.GetValueOrDefault(key) != newFields.GetValueOrDefault(key))
            .Select(key => new FieldDiff(key, oldFields.GetValueOrDefault(key), newFields.GetValueOrDefault(key)))
            .ToArray();
    }

    private static Dictionary<string, string?> Fields(IngredientValue value)
    {
        var fields = new Dictionary<string, string?>
        {
            ["Name"] = JsonSerializer.Serialize(value.Name),
            ["Category"] = value.Category is null ? null : MigrationNames.Normalize(value.Category),
            ["ExcludeFromGroceryList"] = value.ExcludeFromGroceryList.ToString()
        };
        foreach (var property in typeof(NutritionValue).GetProperties())
        {
            var scalar = property.GetValue(value.Nutrition);
            fields[$"Nutrition.{property.Name}"] = scalar is decimal number
                ? number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
                : JsonSerializer.Serialize(scalar);
        }
        foreach (var mapping in value.Mappings)
            fields[$"Mappings[{mapping.Store}]"] = JsonSerializer.Serialize(new
            {
                Department = mapping.Department is null ? null : MigrationNames.Normalize(mapping.Department),
                mapping.IsNotStocked
            });
        foreach (var link in value.Links)
            fields[$"Links[{link.Chain}:{link.Position}]"] = JsonSerializer.Serialize(link);
        return fields;
    }
}
