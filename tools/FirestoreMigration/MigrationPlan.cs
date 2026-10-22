using System.Text.Json;

namespace FirestoreMigration;

public sealed record PlannedIngredient(IngredientValue Incoming, IngredientValue? Existing,
    IReadOnlyList<FieldDiff> Diffs);

public sealed class MigrationPlan
{
    public MigrationSource Source { get; }
    public TargetSnapshot Target { get; }
    public List<PlannedIngredient> Ingredients { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool CanWrite => Source.CanWrite && Errors.Count == 0;

    public MigrationPlan(MigrationSource source, TargetSnapshot target)
    {
        Source = source;
        Target = target;
        var existing = target.Ingredients.ToDictionary(row => row.Id);
        foreach (var id in target.OwnershipConflicts)
            Errors.Add($"{id}: ownership conflict.");
        foreach (var decision in source.Decisions.Where(pair => source.SourceIds.Contains(pair.Key)))
        {
            if (decision.Value.Classification != "Ingredient" && existing.ContainsKey(decision.Key))
                Errors.Add($"{decision.Key}: classification conflicts with an existing Ingredient; resolve explicitly.");
        }
        foreach (var code in source.StoreCodes.Distinct())
            if (!target.Stores.Any(store => store.Code == code)) Errors.Add($"Unknown store code '{code}'.");
        foreach (var code in source.ProductChainCodes.Distinct())
            if (!target.Chains.Any(chain => chain.Code == code)) Errors.Add($"Unknown chain code '{code}'.");
        foreach (var incoming in source.Ingredients)
        {
            var before = existing.TryGetValue(incoming.Id, out var row) ? TargetSnapshot.Value(row) : null;
            Ingredients.Add(new(incoming, before, before is null ? [] : IngredientDiff.Compare(before, incoming)));
            WarnMissing(incoming.Category, source.Categories, incoming.Name, "category");
            foreach (var mapping in incoming.Mappings)
                WarnMissing(mapping.Department, source.Departments, incoming.Name, "department");
        }
        ValidateNames(Ingredients.Select(item => item.Incoming));
    }

    private void WarnMissing(string? value, List<string> master, string name, string kind)
    {
        if (value is not null && !master.Any(entry => MigrationNames.Normalize(entry) == MigrationNames.Normalize(value)))
            Warnings.Add($"{JsonSerializer.Serialize(name)}: {kind} {JsonSerializer.Serialize(value)} is absent from meta; will reuse or create if applied.");
    }

    public IReadOnlyList<string> NameConflicts(IEnumerable<IngredientValue> applied)
    {
        var final = Target.Ingredients.ToDictionary(row => row.Id,
            row => (row.Name, Key: MigrationNames.Normalize(row.Name)));
        foreach (var incoming in applied) final[incoming.Id] = (incoming.Name, MigrationNames.Normalize(incoming.Name));
        return final.GroupBy(pair => pair.Value.Key).Where(group => group.Count() > 1)
            .Select(group => $"Unique-name conflict {JsonSerializer.Serialize(group.Key)}: " +
                string.Join(", ", group.Select(pair => pair.Key))).ToArray();
    }

    private void ValidateNames(IEnumerable<IngredientValue> applied) => Errors.AddRange(NameConflicts(applied));

    public void Print(TextWriter output)
    {
        output.WriteLine($"Preview: new={Ingredients.Count(item => item.Existing is null)}, " +
            $"unchanged={Ingredients.Count(item => item.Existing is not null && item.Diffs.Count == 0)}, " +
            $"changed={Ingredients.Count(item => item.Diffs.Count > 0)}, headings={Source.Headings}, notes={Source.Notes}, " +
            $"unclassified={Source.Unclassified.Count}");
        PrintLookups(output, "Categories", Source.Categories.Concat(Source.Ingredients
            .Where(row => row.Category is not null).Select(row => row.Category!)),
            Target.Categories.Select(row => row.NormalizedName));
        PrintLookups(output, "Departments", Source.Departments.Concat(Source.Ingredients
            .SelectMany(row => row.Mappings).Where(row => row.Department is not null).Select(row => row.Department!)),
            Target.Departments.Select(row => row.NormalizedName));
        var mappings = Source.Ingredients.SelectMany(row => row.Mappings).ToArray();
        output.WriteLine($"Relationships: mapped={mappings.Count(row => !row.IsNotStocked)}, " +
            $"not-stocked={mappings.Count(row => row.IsNotStocked)}, " +
            $"unmapped={Source.Ingredients.Sum(row => Target.Stores.Count(store => !row.Mappings.Any(mapping => mapping.Store == store.Code)))}, " +
            $"links={Source.Ingredients.Sum(row => row.Links.Count)}, " +
            $"nutrition={Source.Ingredients.Count(row => row.Nutrition != new NutritionValue())}");
        foreach (var error in Source.Errors.Concat(Errors)) output.WriteLine($"ERROR: {error}");
        foreach (var warning in Source.Warnings.Concat(Warnings)) output.WriteLine($"WARNING: {warning}");
        foreach (var item in Source.Unclassified) output.WriteLine($"UNCLASSIFIED: {item}");
        foreach (var (field, count) in Source.Ignored.OrderBy(pair => pair.Key))
            output.WriteLine($"IGNORED: {field} ({count})");
        foreach (var item in Ingredients.Where(item => item.Diffs.Count > 0)) PrintDiff(output, item);
    }

    public static void PrintDiff(TextWriter output, PlannedIngredient item)
    {
        output.WriteLine($"Ingredient {item.Incoming.Id} {JsonSerializer.Serialize(item.Incoming.Name)}:");
        foreach (var diff in item.Diffs)
            output.WriteLine($"  {diff.Field}: {diff.Before ?? "<absent>"} -> {diff.Incoming ?? "<absent>"}");
    }

    private static void PrintLookups(TextWriter output, string kind, IEnumerable<string> proposed,
        IEnumerable<string> existing)
    {
        var names = proposed.Select(MigrationNames.Normalize).Distinct().ToArray();
        var known = existing.ToHashSet();
        output.WriteLine($"{kind}: new={names.Count(name => !known.Contains(name))}, existing={names.Count(known.Contains)}");
    }
}
