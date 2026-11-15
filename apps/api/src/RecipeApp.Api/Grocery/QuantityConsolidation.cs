namespace RecipeApp.Api.Grocery;

public sealed record KnownQuantityTotal(
    decimal Amount,
    QuantityDimension Dimension,
    string CanonicalUnit,
    string? CountLabel);

public sealed record QuantityConsolidationResult(
    IReadOnlyList<KnownQuantityTotal> KnownTotals,
    IReadOnlyList<QuantityInterpretation> UnresolvedContributions);

public static class QuantityConsolidation
{
    public static QuantityConsolidationResult Consolidate(
        IEnumerable<QuantityInterpretation> contributions)
    {
        var known = new List<QuantityInterpretation>();
        var unresolved = new List<QuantityInterpretation>();
        foreach (var contribution in contributions)
        {
            if (contribution.Status == QuantityInterpretationStatus.Unresolved)
            {
                unresolved.Add(contribution);
                continue;
            }

            if (contribution.Amount is null
                || contribution.Dimension is null
                || string.IsNullOrWhiteSpace(contribution.CanonicalUnit))
            {
                throw new InvalidOperationException("A known quantity must include its amount and dimension.");
            }

            known.Add(contribution);
        }

        var totals = known
            .GroupBy(contribution => (
                Dimension: contribution.Dimension!.Value,
                CanonicalUnit: contribution.CanonicalUnit!,
                contribution.CountLabel))
            .Select(group =>
            {
                var amount = group.Sum(contribution => contribution.Amount!.Value);
                if (!GroceryDecimalContract.CanRepresent(amount))
                {
                    throw new OverflowException("The consolidated quantity exceeds the supported decimal range.");
                }

                return new KnownQuantityTotal(
                    amount,
                    group.Key.Dimension,
                    group.Key.CanonicalUnit,
                    group.Key.CountLabel);
            })
            .OrderBy(total => total.Dimension)
            .ThenBy(total => total.CanonicalUnit, StringComparer.Ordinal)
            .ThenBy(total => total.CountLabel, StringComparer.Ordinal)
            .ToArray();

        return new QuantityConsolidationResult(totals, unresolved);
    }
}
