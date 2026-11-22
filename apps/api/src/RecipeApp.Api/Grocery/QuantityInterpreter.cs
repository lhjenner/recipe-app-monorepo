using System.Text.RegularExpressions;

namespace RecipeApp.Api.Grocery;

public static partial class QuantityInterpreter
{
    public static QuantityInterpretation InterpretRecipe(string? amountText, string? unitText) =>
        InterpretCore(amountText, string.IsNullOrWhiteSpace(unitText) ? "item" : unitText, true)
            with { OriginalUnitText = unitText };

    public static QuantityInterpretation Interpret(string? amountText, string? unitText) =>
        InterpretCore(amountText, unitText, false);

    private static QuantityInterpretation InterpretCore(string? amountText, string? unitText, bool recipe)
    {
        if (!QuantityUnitCatalog.TryGet(unitText, out var definition, out var unitError))
        {
            return Unresolved(amountText, unitText, unitError);
        }

        if (!TryParsePositiveAmount(amountText, recipe, out var amount, out var reason))
        {
            return Unresolved(amountText, unitText, reason);
        }

        var canonical = amount.Multiply(definition.Multiplier);
        var valid = recipe
            ? canonical.TryToRoundedDecimal(
                definition.Dimension == QuantityDimension.Count ? GroceryDecimalContract.Scale : 2,
                out var canonicalAmount, out var failure)
            : canonical.TryToStoredDecimal(out canonicalAmount, out failure);
        if (!valid)
        {
            var failureMessage = recipe && failure == ExactQuantityFailure.Precision
                ? "The quantity is too small to round at the supported precision."
                : FailureReason(failure);
            return Unresolved(amountText, unitText, failureMessage);
        }

        return new QuantityInterpretation(
            QuantityInterpretationStatus.Known,
            amountText,
            unitText,
            canonicalAmount,
            definition.Dimension,
            definition.CanonicalUnit,
            definition.CountLabel,
            null);
    }

    private static bool TryParsePositiveAmount(
        string? amountText,
        bool recipe,
        out ExactQuantityValue amount,
        out string reason)
    {
        amount = default;
        if (string.IsNullOrWhiteSpace(amountText))
        {
            reason = "The quantity is blank.";
            return false;
        }

        var value = amountText.Trim();
        if (!SupportedNumberExpression().IsMatch(value))
        {
            reason = "The quantity is not a complete supported number.";
            return false;
        }

        if (!ExactQuantityValue.TryParse(value, out amount, out var failure))
        {
            reason = FailureReason(failure);
            return false;
        }

        if (amount.Numerator <= 0)
        {
            reason = "The quantity must be greater than zero.";
            return false;
        }

        if (!recipe && !amount.TryToStoredDecimal(out _, out failure))
        {
            reason = FailureReason(failure);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static string FailureReason(ExactQuantityFailure failure) =>
        failure switch
        {
            ExactQuantityFailure.Precision =>
                "The quantity cannot be represented exactly at the supported precision.",
            ExactQuantityFailure.Range => "The quantity exceeds the supported range.",
            _ => "The fraction is invalid or cannot be represented exactly."
        };

    private static QuantityInterpretation Unresolved(
        string? amountText,
        string? unitText,
        string reason) =>
        new(
            QuantityInterpretationStatus.Unresolved,
            amountText,
            unitText,
            null,
            null,
            null,
            null,
            reason);

    [GeneratedRegex(
        @"\A(?:[0-9]+(?:\.[0-9]+)?|[0-9]+\s+[0-9]+\s*/\s*[0-9]+|[0-9]+\s*/\s*[0-9]+)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex SupportedNumberExpression();
}
