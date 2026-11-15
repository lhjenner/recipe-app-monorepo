namespace RecipeApp.Api.Grocery;

public enum QuantityInterpretationStatus
{
    Known,
    Unresolved
}

public sealed record QuantityInterpretation(
    QuantityInterpretationStatus Status,
    string? OriginalAmountText,
    string? OriginalUnitText,
    decimal? Amount,
    QuantityDimension? Dimension,
    string? CanonicalUnit,
    string? CountLabel,
    string? UnresolvedReason);
