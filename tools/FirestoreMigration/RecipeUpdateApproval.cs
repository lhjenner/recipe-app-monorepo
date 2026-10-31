using System.Text.Json;

namespace FirestoreMigration;

public static class RecipeUpdateApproval
{
    public static async Task<IReadOnlySet<string>> CollectAsync(
        RecipePreviewPlan plan,
        IReadOnlyList<RecipeSourceRecord> sourceRecipes,
        IReadOnlyList<RecipeTargetRecord> targets,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var sourceByLegacyId = sourceRecipes.ToDictionary(recipe => recipe.LegacyId, StringComparer.Ordinal);
        var targetByLegacyId = targets.ToDictionary(recipe => recipe.LegacyId, StringComparer.Ordinal);
        var approved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in plan.Recipes.Where(recipe => recipe.Outcome == "proposed-update"))
        {
            if (!sourceByLegacyId.TryGetValue(update.LegacyId, out var source) ||
                !targetByLegacyId.TryGetValue(update.LegacyId, out var target))
                throw new InvalidOperationException(
                    $"Recipe '{update.LegacyId}' could not be resolved for approval.");

            output.WriteLine($"Recipe update review: LegacyId={update.LegacyId}, RecipeId={target.Id}");
            foreach (var difference in update.Differences)
                PrintDifference(output, difference, source, target);
            output.Write("Apply this entire Recipe update? Type 'yes' to approve; anything else declines: ");
            await output.FlushAsync(cancellationToken);
            var response = await input.ReadLineAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
                approved.Add(update.LegacyId);
            else
                output.WriteLine($"Declined Recipe update: LegacyId={update.LegacyId}");
        }
        return approved;
    }

    private static void PrintDifference(TextWriter output, string difference,
        RecipeSourceRecord source, RecipeTargetRecord target)
    {
        switch (difference)
        {
            case "Name":
                PrintValue(output, difference, target.Name, source.Name);
                break;
            case "SourceText":
                PrintValue(output, difference, target.SourceText, source.SourceText);
                break;
            case "SourceUrl":
                PrintValue(output, difference, SafeUrl(target.SourceUrl), SafeUrl(source.SourceUrl));
                break;
            case "ServingsText":
                PrintValue(output, difference, target.ServingsText, source.ServingsText);
                break;
            case "PrepTimeText":
                PrintValue(output, difference, target.PrepTimeText, source.PrepTimeText);
                break;
            case "CookTimeText":
                PrintValue(output, difference, target.CookTimeText, source.CookTimeText);
                break;
            case "DateAddedAtUtc":
                PrintValue(output, difference, target.DateAddedAtUtc, source.DateAddedAtUtc);
                break;
            case "IsArchived":
                PrintValue(output, difference, target.IsArchived, source.IsArchived);
                break;
            case "Tags":
                PrintValue(output, difference, target.Tags, source.Tags);
                break;
            case "Steps":
                PrintSteps(output, target.Steps, source.Steps);
                break;
            case "SourceProvenance":
                output.WriteLine("  SourceProvenance: source JSON changed (retained in provenance; values not printed).");
                break;
            case "Image":
                output.WriteLine("  Image: association added, replaced or removed; checksum/bytes differ from the target.");
                break;
            default:
                output.WriteLine($"  {difference}: changed.");
                break;
        }
    }

    private static void PrintSteps(TextWriter output,
        IReadOnlyList<RecipeTargetStep> existing, IReadOnlyList<RecipeSourceStep> incoming)
    {
        output.WriteLine("  Steps:");
        output.WriteLine($"    existing: {JsonSerializer.Serialize(existing)}");
        output.WriteLine($"    proposed: {JsonSerializer.Serialize(incoming.Select(step => new
        {
            step.Position,
            step.Instructions,
            Items = step.Items.Select(item => new
            {
                item.Position,
                Kind = item.Kind.ToString(),
                item.IngredientId,
                item.Text,
                item.QuantityText,
                item.UnitText,
                item.NotesText
            })
        }))}");
    }

    private static void PrintValue<T>(TextWriter output, string field, T existing, T proposed) =>
        output.WriteLine($"  {field}: {JsonSerializer.Serialize(existing)} -> {JsonSerializer.Serialize(proposed)}");

    private static string? SafeUrl(string? value)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return value;
        var authority = uri.IsDefaultPort ? uri.Authority : $"{uri.Host}:{uri.Port}";
        var suffix = string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            ? ""
            : "?<redacted>";
        return $"{uri.Scheme}://{authority}{uri.AbsolutePath}{suffix}";
    }
}
