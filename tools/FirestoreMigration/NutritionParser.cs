using System.Text.Json;

namespace FirestoreMigration;

internal static class NutritionParser
{
    public static NutritionValue Read(JsonElement ingredient, string name, MigrationSource source)
    {
        if (!ingredient.TryGetProperty("nutrition", out var nutrition) || nutrition.ValueKind == JsonValueKind.Null)
            return new();
        JsonInput.Require(nutrition, JsonValueKind.Object, "nutrition");
        JsonInput.Ignored(nutrition, "ingredients.nutrition", source, "per100g", "source", "lastUpdated");
        var values = new Dictionary<string, decimal?>();
        string[] fields = ["calories", "protein", "carbohydrates", "sugar", "fat", "saturatedFat", "fiber", "sodium"];
        if (nutrition.TryGetProperty("per100g", out var per100g) && per100g.ValueKind != JsonValueKind.Null)
        {
            JsonInput.Require(per100g, JsonValueKind.Object, "nutrition.per100g");
            JsonInput.Ignored(per100g, "ingredients.nutrition.per100g", source, fields);
            foreach (var field in fields)
            {
                if (!per100g.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) continue;
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number) ||
                    number is < -999999.999m or > 999999.999m || !HasExactScale(value.GetRawText()))
                    throw new FormatException($"{name}: nutrition.{field} cannot be represented exactly as numeric(9,3).");
                values[field] = number;
                var maximum = field == "calories" ? 900 : field == "sodium" ? 40000 : 100;
                if (number < 0 || number > maximum)
                    source.Warnings.Add($"{name}: nutrition.{field} is outside 0-{maximum}; source value retained.");
            }
        }
        decimal? Value(string key) => values.GetValueOrDefault(key);
        if (Value("saturatedFat") > Value("fat"))
            source.Warnings.Add($"{name}: saturated fat exceeds fat; source values retained.");
        if (Value("sugar") > Value("carbohydrates"))
            source.Warnings.Add($"{name}: sugar exceeds carbohydrate; source values retained.");
        return new(Value("calories"), Value("protein"), Value("carbohydrates"), Value("sugar"),
            Value("fat"), Value("saturatedFat"), Value("fiber"), Value("sodium"),
            JsonInput.OptionalText(nutrition, "source"), Timestamp(nutrition, name, source));
    }

    private static bool HasExactScale(string raw)
    {
        var parts = raw.Split(['e', 'E']);
        var mantissa = parts[0].TrimStart('-');
        var dot = mantissa.IndexOf('.');
        var fractionDigits = dot < 0 ? 0 : mantissa.Length - dot - 1;
        var digits = mantissa.Replace(".", "");
        if (digits.All(digit => digit == '0')) return true;
        if (parts.Length == 2 && (!long.TryParse(parts[1], out var exponent) ||
            exponent is < -1000000 or > 1000000)) return false;
        var power = parts.Length == 2 ? long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        var trailingZeros = digits.Length - digits.TrimEnd('0').Length;
        return fractionDigits - power - trailingZeros <= 3;
    }

    private static DateTime? Timestamp(JsonElement nutrition, string name, MigrationSource source)
    {
        if (!nutrition.TryGetProperty("lastUpdated", out var timestamp) || timestamp.ValueKind == JsonValueKind.Null)
            return null;
        JsonInput.Require(timestamp, JsonValueKind.Object, "nutrition.lastUpdated");
        if (!timestamp.TryGetProperty("_seconds", out var seconds) || seconds.ValueKind != JsonValueKind.Number ||
            !seconds.TryGetInt64(out var secondsValue) ||
            !timestamp.TryGetProperty("_nanoseconds", out var nanos) || nanos.ValueKind != JsonValueKind.Number ||
            !nanos.TryGetInt32(out var nanosValue) ||
            nanosValue is < 0 or >= 1000000000)
            throw new FormatException($"{name}: invalid Firestore timestamp.");
        // PostgreSQL timestamps have microsecond precision; never silently round the source.
        if (nanosValue % 1000 != 0)
            throw new FormatException($"{name}: nutrition timestamp exceeds PostgreSQL microsecond precision.");
        JsonInput.Ignored(timestamp, "ingredients.nutrition.lastUpdated", source, "_seconds", "_nanoseconds");
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(secondsValue).AddTicks(nanosValue / 100).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException($"{name}: nutrition timestamp is outside the supported range.", exception);
        }
    }
}
