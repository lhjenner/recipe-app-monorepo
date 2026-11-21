using System.Text.RegularExpressions;

namespace RecipeApp.Api.Grocery;

public static partial class QuantityInterpreter
{
    public static QuantityInterpretation InterpretRecipe(string? amountText, string? unitText) =>
        Interpret(amountText, string.IsNullOrWhiteSpace(unitText) ? "item" : unitText)
            with { OriginalUnitText = unitText };

    public static QuantityInterpretation Interpret(string? amountText, string? unitText)
    {
        if (!QuantityUnitCatalog.TryGet(unitText, out var definition, out var unitError))
        {
            return Unresolved(amountText, unitText, unitError);
        }

        if (!TryParsePositiveAmount(amountText, out var amount, out var reason))
        {
            return Unresolved(amountText, unitText, reason);
        }

        if (!amount.Multiply(definition.Multiplier)
                .TryToStoredDecimal(out var canonicalAmount, out var failure))
        {
            return Unresolved(amountText, unitText, FailureReason(failure));
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

        if (!amount.TryToStoredDecimal(out _, out failure))
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
