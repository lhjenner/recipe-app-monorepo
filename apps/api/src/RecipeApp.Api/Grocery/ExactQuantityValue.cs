using System.Globalization;
using System.Numerics;

namespace RecipeApp.Api.Grocery;

internal enum ExactQuantityFailure
{
    None,
    Invalid,
    Precision,
    Range
}

internal readonly record struct ExactQuantityValue(BigInteger Numerator, BigInteger Denominator)
{
    private static readonly BigInteger MaximumDecimalInteger =
        BigInteger.Parse(decimal.MaxValue.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    private static readonly BigInteger StorageScale = BigInteger.Pow(10, GroceryDecimalContract.Scale);
    private static readonly BigInteger MaximumStoredScaledAmount =
        new(GroceryDecimalContract.MaximumStoredAmount * 1_000_000_000m);

    public static bool TryParse(
        string value,
        out ExactQuantityValue quantity,
        out ExactQuantityFailure failure)
    {
        quantity = default;
        failure = ExactQuantityFailure.Invalid;
        var slash = value.IndexOf('/');
        if (slash >= 0)
        {
            var wholeAndNumerator = value[..slash]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var denominator = value[(slash + 1)..];
            if (wholeAndNumerator.Length == 1)
            {
                return TryParseFraction(
                    wholeAndNumerator[0],
                    denominator,
                    out quantity,
                    out failure);
            }

            if (wholeAndNumerator.Length == 2
                && TryParseUnsignedInteger(wholeAndNumerator[0], out var whole, out failure)
                && TryParseFraction(
                    wholeAndNumerator[1],
                    denominator,
                    out var fraction,
                    out failure))
            {
                quantity = new(
                    whole * fraction.Denominator + fraction.Numerator,
                    fraction.Denominator);
                return true;
            }

            if (wholeAndNumerator.Length != 2)
            {
                failure = ExactQuantityFailure.Invalid;
            }

            return false;
        }

        return TryParseDecimal(value, out quantity, out failure);
    }

    public ExactQuantityValue Multiply(decimal multiplier)
    {
        var bits = decimal.GetBits(multiplier);
        var coefficient = new BigInteger((uint)bits[0])
            + (new BigInteger((uint)bits[1]) << 32)
            + (new BigInteger((uint)bits[2]) << 64);
        if ((bits[3] & int.MinValue) != 0)
        {
            coefficient = -coefficient;
        }

        var scale = (bits[3] >> 16) & 0x7f;
        return new(
            Numerator * coefficient,
            Denominator * BigInteger.Pow(10, scale));
    }

    public bool TryToStoredDecimal(out decimal amount, out ExactQuantityFailure failure)
    {
        amount = 0;
        if (Denominator <= 0 || Numerator < 0)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        var scaledNumerator = Numerator * StorageScale;
        if (scaledNumerator > MaximumStoredScaledAmount * Denominator)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        var scaledAmount = BigInteger.DivRem(scaledNumerator, Denominator, out var remainder);
        if (remainder != 0)
        {
            failure = ExactQuantityFailure.Precision;
            return false;
        }

        amount = (decimal)scaledAmount / 1_000_000_000m;
        failure = ExactQuantityFailure.None;
        return true;
    }

    private static bool TryParseFraction(
        string numeratorText,
        string denominatorText,
        out ExactQuantityValue quantity,
        out ExactQuantityFailure failure)
    {
        quantity = default;
        if (!TryParseUnsignedInteger(numeratorText, out var numerator, out failure)
            || !TryParseUnsignedInteger(denominatorText, out var denominator, out failure))
        {
            return false;
        }

        if (numerator <= 0 || denominator <= 0)
        {
            failure = ExactQuantityFailure.Invalid;
            return false;
        }

        quantity = new(numerator, denominator);
        failure = ExactQuantityFailure.None;
        return true;
    }

    private static bool TryParseDecimal(
        string value,
        out ExactQuantityValue quantity,
        out ExactQuantityFailure failure)
    {
        quantity = default;
        var separator = value.IndexOf('.');
        var integerText = separator < 0 ? value : value[..separator];
        var fractionalText = separator < 0 ? string.Empty : value[(separator + 1)..].TrimEnd('0');
        if (fractionalText.Length > GroceryDecimalContract.Scale)
        {
            failure = ExactQuantityFailure.Precision;
            return false;
        }

        var significantInteger = integerText.TrimStart('0');
        if (significantInteger.Length > 29)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        significantInteger = significantInteger.Length == 0 ? "0" : significantInteger;
        var digits = significantInteger + fractionalText;
        if (!BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var numerator))
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        quantity = new(numerator, BigInteger.Pow(10, fractionalText.Length));
        failure = ExactQuantityFailure.None;
        return true;
    }

    private static bool TryParseUnsignedInteger(
        string value,
        out BigInteger integer,
        out ExactQuantityFailure failure)
    {
        integer = 0;
        var significantDigits = value.Trim().TrimStart('0');
        if (significantDigits.Length == 0)
        {
            failure = ExactQuantityFailure.None;
            return true;
        }

        if (significantDigits.Length > 29
            || !BigInteger.TryParse(
                significantDigits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out integer)
            || integer > MaximumDecimalInteger)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        failure = ExactQuantityFailure.None;
        return true;
    }
}
