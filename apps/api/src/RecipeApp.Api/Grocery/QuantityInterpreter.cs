using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeApp.Api.Grocery;

public static partial class QuantityInterpreter
{
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

        try
        {
            var canonicalAmount = amount *             definition.Multiplier;
            if (!HasSupportedPrecision(canonicalAmount))
            {
                return Unresolved(
                    amountText,
                    unitText,
                    "The quantity cannot be represented exactly at the supported precision.");
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
        catch (OverflowException)
        {
            return Unresolved(amountText, unitText, "The quantity exceeds the supported range.");
        }
    }

    private static bool TryParsePositiveAmount(
        string? amountText,
        out decimal amount,
        out string reason)
    {
        amount = 0;
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

        try
        {
            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].Contains('/'))
            {
                if (!TryParseFraction(parts[1], out var fraction))
                {
                    reason = "The fraction is invalid or cannot be represented exactly.";
                    return false;
                }

                if (!decimal.TryParse(
                        parts[0],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var whole))
                {
                    reason = "The quantity exceeds the supported range.";
                    return false;
                }

                amount = whole + fraction;
            }
            else if (value.Contains('/'))
            {
                if (!TryParseFraction(value, out amount))
                {
                    reason = "The fraction is invalid or cannot be represented exactly.";
                    return false;
                }
            }
            else if (!decimal.TryParse(
                         value,
                         NumberStyles.AllowDecimalPoint,
                         CultureInfo.InvariantCulture,
                         out amount))
            {
                reason = "The quantity exceeds the supported range.";
                return false;
            }
        }
        catch (OverflowException)
        {
            reason = "The quantity exceeds the supported range.";
            return false;
        }

        if (amount <= 0)
        {
            reason = "The quantity must be greater than zero.";
            return false;
        }

        if (!HasSupportedPrecision(amount))
        {
            reason = "The quantity cannot be represented exactly at the supported precision.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryParseFraction(string value, out decimal amount)
    {
        amount = 0;
        var parts = value.Split('/');
        if (parts.Length != 2
            || !decimal.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            || !decimal.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var denominator)
            || numerator <= 0
            || denominator <= 0)
        {
            return false;
        }

        amount = numerator / denominator;
        return HasSupportedPrecision(amount);
    }

    private static bool HasSupportedPrecision(decimal amount) =>
        GroceryDecimalContract.CanRepresent(amount);

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
