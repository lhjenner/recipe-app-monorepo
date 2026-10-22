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

public sealed record MigrationOptions(string Backup, Guid Owner, string Decisions, bool Write)
{
    public const string Usage =
        "Usage: --backup <directory> --owner <GUID> --decisions <file> [--write]. Default: dry-run.";

    public static MigrationOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        var write = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--write")
            {
                if (write) throw new FormatException("Duplicate --write.");
                write = true;
                continue;
            }
            if (args[i] is not ("--backup" or "--owner" or "--decisions") ||
                i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[i], args[++i]))
                throw new FormatException(Usage);
        }
        if (!values.TryGetValue("--backup", out var backup) || string.IsNullOrWhiteSpace(backup) ||
            !values.TryGetValue("--decisions", out var decisions) || string.IsNullOrWhiteSpace(decisions) ||
            !values.TryGetValue("--owner", out var owner) || !Guid.TryParse(owner, out var id) || id == Guid.Empty)
            throw new FormatException(Usage);
        return new(Path.GetFullPath(backup), id, Path.GetFullPath(decisions), write);
    }
}
