using System.Numerics;

namespace RecipeApp.Api.Grocery;

internal readonly partial record struct ExactQuantityValue
{
    public ExactQuantityValue Add(ExactQuantityValue other)
    {
        var divisor = BigInteger.GreatestCommonDivisor(Denominator, other.Denominator);
        var numerator = Numerator * (other.Denominator / divisor)
            + other.Numerator * (Denominator / divisor);
        var denominator = Denominator * (other.Denominator / divisor);
        var reduction = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return new(numerator / reduction, denominator / reduction);
    }

    public bool TryToRoundedDecimal(int scale, out decimal amount, out ExactQuantityFailure failure)
    {
        amount = 0;
        if (Denominator <= 0 || Numerator < 0
            || Numerator * StorageScale > MaximumStoredScaledAmount * Denominator)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        var factor = BigInteger.Pow(10, scale);
        var rounded = BigInteger.DivRem(Numerator * factor, Denominator, out var remainder);
        if (remainder * 2 >= Denominator)
        {
            rounded++;
        }

        if (rounded == 0 && Numerator > 0)
        {
            failure = ExactQuantityFailure.Precision;
            return false;
        }

        if (rounded * StorageScale > MaximumStoredScaledAmount * factor)
        {
            failure = ExactQuantityFailure.Range;
            return false;
        }

        amount = (decimal)rounded / (decimal)factor;
        failure = ExactQuantityFailure.None;
        return true;
    }

    public bool TryCeiling(out decimal amount)
    {
        amount = 0;
        if (Denominator <= 0 || Numerator < 0)
        {
            return false;
        }

        var integer = BigInteger.DivRem(Numerator, Denominator, out var remainder);
        if (remainder != 0)
        {
            integer++;
        }

        if (integer * StorageScale > MaximumStoredScaledAmount)
        {
            return false;
        }

        amount = (decimal)integer;
        return true;
    }
}
