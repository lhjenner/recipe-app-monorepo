using RecipeApp.Api.Lookups;

namespace FirestoreMigration;

public sealed record ClassificationDecision(string Name, string Classification);
public sealed record MappingValue(string Store, string? Department, bool IsNotStocked);
public sealed record LinkValue(string Chain, int Position, string Url, string? VariantLabel,
    string? Note, bool SupportsBothPricingMethods, bool IsPreferred);
public sealed record NutritionValue(decimal? EnergyKcal = null, decimal? ProteinG = null,
    decimal? CarbohydrateG = null, decimal? SugarG = null, decimal? FatG = null,
    decimal? SaturatedFatG = null, decimal? FibreG = null, decimal? SodiumMg = null,
    string? Source = null, DateTime? UpdatedAtUtc = null);
public sealed record IngredientValue(Guid Id, string Name, string? Category,
    bool ExcludeFromGroceryList, IReadOnlyList<MappingValue> Mappings,
    IReadOnlyList<LinkValue> Links, NutritionValue Nutrition);

public sealed class MigrationSource
{
    public List<IngredientValue> Ingredients { get; } = [];
    public Dictionary<Guid, ClassificationDecision> Decisions { get; } = [];
    public HashSet<Guid> SourceIds { get; } = [];
    public List<string> Categories { get; } = [];
    public List<string> Departments { get; } = [];
    public List<string> StoreCodes { get; } = [];
    public List<string> ProductChainCodes { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Unclassified { get; } = [];
    public Dictionary<string, int> Ignored { get; } = [];
    public int Headings { get; set; }
    public int Notes { get; set; }
    public bool CanWrite => Errors.Count == 0 && Unclassified.Count == 0;
}

public static class MigrationNames
{
    public static string Normalize(string value) => LookupName.Normalize(value);
    public static string Clean(string value) => LookupName.Clean(value);
}

public enum MigrationMode
{
    Ingredients,
    Recipes
}

public sealed record MigrationOptions(string Backup, Guid Owner, string Decisions, bool Write,
    MigrationMode Mode = MigrationMode.Ingredients, string? RecipeFile = null,
    string? ReportFile = null, IReadOnlySet<string>? SelectedLegacyOwnerIds = null)
{
    public const string Usage =
        "Ingredient mode: --backup <directory> --owner <GUID> --decisions <file> [--write]. Default mode: ingredients; default action: dry-run.\n" +
        "Recipe mode: --mode recipes --recipes <file> --owner <GUID> --decisions <file> --report <file> " +
        "[--source-owner <legacy ID> ...]. Recipe writes are not available.";

    public static MigrationOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        var sourceOwners = new HashSet<string>(StringComparer.Ordinal);
        var write = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--write")
            {
                if (write) throw new FormatException("Duplicate --write.");
                write = true;
                continue;
            }
            if (args[i] == "--source-owner")
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(args[i + 1]) || !sourceOwners.Add(args[++i]))
                    throw new FormatException("--source-owner requires a non-empty, unique legacy owner ID.");
                continue;
            }
            if (args[i] is not ("--mode" or "--backup" or "--recipes" or "--owner" or "--decisions" or "--report") ||
                i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[i], args[++i]))
                throw new FormatException(Usage);
        }
        var mode = values.GetValueOrDefault("--mode") switch
        {
            null or "ingredients" => MigrationMode.Ingredients,
            "recipes" => MigrationMode.Recipes,
            _ => throw new FormatException("--mode must be either 'ingredients' or 'recipes'.")
        };
        var hasBackup = values.TryGetValue("--backup", out var backup) && !string.IsNullOrWhiteSpace(backup);
        var hasRecipeFile = values.TryGetValue("--recipes", out var recipeFile) && !string.IsNullOrWhiteSpace(recipeFile);
        var hasReport = values.TryGetValue("--report", out var reportFile) && !string.IsNullOrWhiteSpace(reportFile);
        if (mode == MigrationMode.Ingredients && (!hasBackup || hasRecipeFile) ||
            mode == MigrationMode.Recipes && (!hasRecipeFile || hasBackup) ||
            mode == MigrationMode.Recipes && (!hasReport || write) ||
            mode == MigrationMode.Ingredients && (hasReport || sourceOwners.Count > 0) ||
            !values.TryGetValue("--decisions", out var decisions) || string.IsNullOrWhiteSpace(decisions) ||
            !values.TryGetValue("--owner", out var owner) || !Guid.TryParse(owner, out var id) || id == Guid.Empty)
            throw new FormatException(Usage);
        return new(mode == MigrationMode.Ingredients ? Path.GetFullPath(backup!) : string.Empty,
            id, Path.GetFullPath(decisions!), write, mode,
            mode == MigrationMode.Recipes ? Path.GetFullPath(recipeFile!) : null,
            mode == MigrationMode.Recipes ? Path.GetFullPath(reportFile!) : null,
            sourceOwners.Count == 0 ? null : sourceOwners);
    }
}
