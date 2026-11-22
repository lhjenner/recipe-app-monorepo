using System.Globalization;
using RecipeApp.Api.Ingredients;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryManualEntryProcessor
{
    public static IReadOnlyList<GroceryDuplicateReview> BuildDuplicateReviews(
        IReadOnlyList<PreparedManualEntry> entries,
        IReadOnlyList<GroceryItem> activeItems)
    {
        var targets = new Dictionary<string, DuplicateTarget>(StringComparer.Ordinal);
        var measuredTargets = activeItems
            .Where(IsMeasuredRecipeTarget)
            .GroupBy(item => SuggestedTargetKey(item.IngredientId!.Value, item.PurchaseUnit), StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var item = group.Single();
                    return new DuplicateTarget(item.Id, null, item.OutstandingPurchaseQuantity, item.NameSnapshot);
                },
                StringComparer.Ordinal);
        foreach (var item in activeItems)
        {
            targets.Add(TargetKey(item.ConsolidationKey, item.PurchaseUnit),
                new DuplicateTarget(item.Id, null, item.OutstandingPurchaseQuantity, item.NameSnapshot));
        }

        var reviews = new List<GroceryDuplicateReview>();
        foreach (var entry in entries)
        {
            var targetKey = TargetKey(GetConsolidationKey(entry), entry.CanonicalUnit);
            if (!targets.ContainsKey(targetKey)
                && IsGenericItemTarget(entry)
                && measuredTargets.TryGetValue(
                    SuggestedTargetKey(entry.IngredientId!.Value, entry.CanonicalUnit),
                    out var suggestedTarget))
            {
                targets.Add(targetKey, suggestedTarget);
            }

            if (targets.TryGetValue(targetKey, out var existing))
            {
                var resulting = existing.Quantity + entry.Input.PurchaseQuantity;
                reviews.Add(new GroceryDuplicateReview(
                    entry.Index,
                    existing.ItemId,
                    existing.EntryIndex,
                    existing.Quantity,
                    entry.Input.PurchaseQuantity,
                    resulting,
                    existing.Name,
                    entry.CanonicalUnit));
                targets[targetKey] = existing with { Quantity = resulting };
            }
            else
            {
                targets.Add(targetKey, new DuplicateTarget(
                    null, entry.Index, entry.Input.PurchaseQuantity, entry.Name));
            }
        }

        return reviews;
    }

    public static IReadOnlyList<Guid> ApplyEntries(
        AppDbContext database,
        GroceryList list,
        IReadOnlyList<PreparedManualEntry> entries,
        IReadOnlyList<GroceryItem> activeItems,
        IReadOnlyList<GroceryDuplicateReview> reviews,
        DateTime now)
    {
        var existingById = activeItems.ToDictionary(item => item.Id);
        var reviewByIndex = reviews.ToDictionary(review => review.EntryIndex);
        var itemByEntryIndex = new Dictionary<int, GroceryItem>();
        var resultItems = new Dictionary<Guid, GroceryItem>();
        foreach (var entry in entries)
        {
            GroceryItem item;
            if (reviewByIndex.TryGetValue(entry.Index, out var review))
            {
                item = review.TargetItemId is not null
                    ? existingById[review.TargetItemId.Value]
                    : itemByEntryIndex[review.TargetEntryIndex!.Value];
            }
            else
            {
                item = new GroceryItem
                {
                    GroceryListId = list.Id,
                    OwnerUserId = list.OwnerUserId,
                    IngredientId = entry.IngredientId,
                    NameSnapshot = entry.Name,
                    NormalizedName = entry.NormalizedName,
                    Note = CleanOptional(entry.Input.Note),
                    StoreId = entry.IngredientId is null ? entry.Input.AdHocStoreId : null,
                    DepartmentId = entry.IngredientId is null ? entry.Input.AdHocDepartmentId : null,
                    Dimension = entry.Dimension,
                    CountLabel = entry.CountLabel,
                    ConsolidationKey = GetConsolidationKey(entry),
                    PurchaseUnit = entry.CanonicalUnit,
                    OutstandingPurchaseQuantity = 0,
                    State = GroceryItemState.Outstanding,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                database.GroceryItems.Add(item);
            }

            item.OutstandingPurchaseQuantity += entry.Input.PurchaseQuantity;
            item.UpdatedAtUtc = now;
            item.Contributions.Add(new GroceryContribution
            {
                GroceryListId = list.Id,
                GroceryItemId = item.Id,
                OwnerUserId = list.OwnerUserId,
                Origin = GroceryContributionOrigin.Manual,
                OriginalNameText = entry.Name,
                OriginalAmountText = entry.Input.PurchaseQuantity.ToString(CultureInfo.InvariantCulture),
                OriginalUnitText = entry.Input.PurchaseUnit?.Trim(),
                OriginalNotesText = CleanOptional(entry.Input.Note),
                Dimension = entry.Dimension,
                CountLabel = entry.CountLabel,
                ManualPurchaseIncrement = entry.Input.PurchaseQuantity,
                State = GroceryContributionState.Outstanding,
                CreatedAtUtc = now
            });
            itemByEntryIndex[entry.Index] = item;
            resultItems[item.Id] = item;
        }

        return resultItems.Keys.ToArray();
    }

    private static string GetConsolidationKey(PreparedManualEntry entry) =>
        entry.IngredientId is not null
            ? GroceryConsolidationKey.ForIngredient(
                entry.IngredientId.Value, entry.Dimension, entry.CountLabel)
            : GroceryConsolidationKey.ForName(entry.Name, entry.Dimension, entry.CountLabel);

    private static string TargetKey(string consolidationKey, string purchaseUnit) =>
        $"{consolidationKey}:{IngredientWriteValidator.Normalize(purchaseUnit)}";

    private static string SuggestedTargetKey(Guid ingredientId, string purchaseUnit) =>
        $"{ingredientId:N}:{IngredientWriteValidator.Normalize(purchaseUnit)}";

    private static bool IsGenericItemTarget(PreparedManualEntry entry) =>
        entry.IngredientId is not null
        && entry.Dimension == QuantityDimension.Count
        && string.Equals(entry.CountLabel, "item", StringComparison.Ordinal)
        && string.Equals(entry.CanonicalUnit, "item", StringComparison.Ordinal);

    private static bool IsMeasuredRecipeTarget(GroceryItem item) =>
        item.IngredientId is not null
        && item.PurchaseUnit == "item"
        && item.State == GroceryItemState.Outstanding
        && item.Dimension is QuantityDimension.Mass or QuantityDimension.Volume or null
        && item.Contributions.Any(contribution => contribution.Origin == GroceryContributionOrigin.Recipe);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DuplicateTarget(
        Guid? ItemId,
        int? EntryIndex,
        decimal Quantity,
        string Name);
}
