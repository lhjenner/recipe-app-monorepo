namespace RecipeApp.Api.Grocery;

internal readonly record struct QuantityUnitDefinition(
    QuantityDimension Dimension,
    string CanonicalUnit,
    decimal Multiplier,
    string? CountLabel = null);

internal static class QuantityUnitCatalog
{
    private static readonly IReadOnlyDictionary<string, QuantityUnitDefinition> Units =
        CreateUnitDefinitions();

    public static bool TryGet(
        string? unitText,
        out QuantityUnitDefinition definition,
        out string error)
    {
        definition = default;
        var unit = Normalize(unitText);
        if (unit.Length == 0)
        {
            error = "A unit is required.";
            return false;
        }

        if (HasAlternativeStandard(unit))
        {
            error = "The stated measurement standard is not NZ.";
            return false;
        }

        if (!Units.TryGetValue(unit, out definition))
        {
            error = "The unit is not supported.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static IReadOnlyDictionary<string, QuantityUnitDefinition> CreateUnitDefinitions()
    {
        var units = new Dictionary<string, QuantityUnitDefinition>(StringComparer.Ordinal)
        {
            ["g"] = new(QuantityDimension.Mass, "g", 1),
            ["gram"] = new(QuantityDimension.Mass, "g", 1),
            ["grams"] = new(QuantityDimension.Mass, "g", 1),
            ["kg"] = new(QuantityDimension.Mass, "g", 1_000),
            ["kilogram"] = new(QuantityDimension.Mass, "g", 1_000),
            ["kilograms"] = new(QuantityDimension.Mass, "g", 1_000),
            ["ml"] = new(QuantityDimension.Volume, "ml", 1),
            ["millilitre"] = new(QuantityDimension.Volume, "ml", 1),
            ["millilitres"] = new(QuantityDimension.Volume, "ml", 1),
            ["l"] = new(QuantityDimension.Volume, "ml", 1_000),
            ["litre"] = new(QuantityDimension.Volume, "ml", 1_000),
            ["litres"] = new(QuantityDimension.Volume, "ml", 1_000),
            ["tsp"] = new(QuantityDimension.Volume, "ml", 5),
            ["teaspoon"] = new(QuantityDimension.Volume, "ml", 5),
            ["teaspoons"] = new(QuantityDimension.Volume, "ml", 5),
            ["tbsp"] = new(QuantityDimension.Volume, "ml", 15),
            ["tablespoon"] = new(QuantityDimension.Volume, "ml", 15),
            ["tablespoons"] = new(QuantityDimension.Volume, "ml", 15),
            ["cup"] = new(QuantityDimension.Volume, "ml", 250),
            ["cups"] = new(QuantityDimension.Volume, "ml", 250)
        };

        AddCountAliases(units,
            ("piece", "pieces"), ("item", "items"), ("whole", "wholes"),
            ("clove", "cloves"), ("bunch", "bunches"), ("tin", "tins"), ("can", "cans"),
            ("packet", "packets"), ("pack", "packs"), ("bag", "bags"), ("jar", "jars"),
            ("bottle", "bottles"), ("box", "boxes"), ("slice", "slices"), ("sprig", "sprigs"),
            ("leaf", "leaves"), ("bulb", "bulbs"), ("head", "heads"), ("stalk", "stalks"),
            ("stick", "sticks"), ("fillet", "fillets"), ("breast", "breasts"), ("egg", "eggs"),
            ("onion", "onions"), ("mushroom", "mushrooms"), ("potato", "potatoes"),
            ("tomato", "tomatoes"), ("apple", "apples"), ("banana", "bananas"),
            ("carrot", "carrots"), ("pepper", "peppers"));

        return units;
    }

    private static void AddCountAliases(
        IDictionary<string, QuantityUnitDefinition> units,
        params (string Singular, string Plural)[] labels)
    {
        foreach (var (singular, plural) in labels)
        {
            var definition = new QuantityUnitDefinition(
                QuantityDimension.Count,
                singular,
                1,
                singular);
            units[singular] = definition;
            units[plural] = definition;
        }
    }

    private static string Normalize(string? unitText) =>
        string.Join(' ', (unitText ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private static bool HasAlternativeStandard(string unit) =>
        unit.StartsWith("us ", StringComparison.Ordinal)
        || unit.StartsWith("uk ", StringComparison.Ordinal)
        || unit.StartsWith("imperial ", StringComparison.Ordinal)
        || unit.StartsWith("australian ", StringComparison.Ordinal);
}
