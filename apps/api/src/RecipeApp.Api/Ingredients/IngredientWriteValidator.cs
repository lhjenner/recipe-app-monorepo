namespace RecipeApp.Api.Ingredients;

internal static class IngredientWriteValidator
{
    public static Dictionary<string, string[]> Validate(IngredientWriteRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var name = Clean(request.Name);
        if (name.Length == 0)
        {
            Add(errors, nameof(request.Name), "Name is required.");
        }
        else if (name.Length > 200)
        {
            Add(errors, nameof(request.Name), "Name must be 200 characters or fewer.");
        }

        ValidateNutrition(request.Nutrition, errors);
        ValidateMappings(request.StoreMappings, errors);
        ValidateProductLinks(request.ProductLinks, errors);

        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }

    public static string Clean(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string value) => Clean(value).ToLowerInvariant();

    private static void ValidateNutrition(
        IngredientNutritionRequest? nutrition,
        Dictionary<string, List<string>> errors)
    {
        if (nutrition is null)
        {
            return;
        }

        Check(nutrition.EnergyKcal, 900, "Nutrition.EnergyKcal", errors);
        Check(nutrition.ProteinG, 100, "Nutrition.ProteinG", errors);
        Check(nutrition.CarbohydrateG, 100, "Nutrition.CarbohydrateG", errors);
        Check(nutrition.SugarG, 100, "Nutrition.SugarG", errors);
        Check(nutrition.FatG, 100, "Nutrition.FatG", errors);
        Check(nutrition.SaturatedFatG, 100, "Nutrition.SaturatedFatG", errors);
        Check(nutrition.FibreG, 100, "Nutrition.FibreG", errors);
        Check(nutrition.SodiumMg, 40_000, "Nutrition.SodiumMg", errors);
        if (nutrition.SaturatedFatG > nutrition.FatG)
        {
            Add(errors, "Nutrition.SaturatedFatG", "Saturated fat cannot exceed total fat.");
        }

        if (nutrition.SugarG > nutrition.CarbohydrateG)
        {
            Add(errors, "Nutrition.SugarG", "Sugar cannot exceed carbohydrate.");
        }
    }

    private static void Check(
        decimal? value,
        decimal maximum,
        string field,
        Dictionary<string, List<string>> errors)
    {
        if (value < 0)
        {
            Add(errors, field, "Value cannot be negative.");
        }
        else if (value > maximum)
        {
            Add(errors, field, $"Value cannot exceed {maximum}.");
        }
        else if (value is not null && decimal.Round(value.Value, 3) != value.Value)
        {
            Add(errors, field, "Value cannot have more than three decimal places.");
        }
    }

    private static void ValidateMappings(
        IReadOnlyList<IngredientStoreMappingRequest> mappings,
        Dictionary<string, List<string>> errors)
    {
        if (mappings.Select(item => item.StoreId).Distinct().Count() != mappings.Count)
        {
            Add(errors, nameof(IngredientWriteRequest.StoreMappings), "Each store may appear only once.");
        }

        for (var index = 0; index < mappings.Count; index++)
        {
            var mapping = mappings[index];
            if (mapping.IsNotStocked && mapping.DepartmentId is not null)
            {
                Add(errors, $"StoreMappings[{index}]", "A not-stocked store cannot have a department.");
            }
        }
    }

    private static void ValidateProductLinks(
        IReadOnlyList<IngredientProductLinkRequest> links,
        Dictionary<string, List<string>> errors)
    {
        var preferredChains = links
            .Where(link => link.IsPreferred)
            .GroupBy(link => link.ChainId)
            .Where(group => group.Count() > 1);
        foreach (var group in preferredChains)
        {
            Add(errors, nameof(IngredientWriteRequest.ProductLinks),
                $"Only one product link per chain may be preferred ({group.Key}).");
        }

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];
            var field = $"ProductLinks[{index}].Url";
            if (link.Url is null
                || link.Url.Length > 2048
                || !Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                Add(errors, field, "URL must be an absolute HTTPS URL of 2048 characters or fewer.");
            }

            if (link.VariantLabel?.Length > 200)
            {
                Add(errors, $"ProductLinks[{index}].VariantLabel", "Variant label must be 200 characters or fewer.");
            }

            if (link.Note?.Length > 1000)
            {
                Add(errors, $"ProductLinks[{index}].Note", "Note must be 1000 characters or fewer.");
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors.Add(field, messages);
        }

        messages.Add(message);
    }
}
