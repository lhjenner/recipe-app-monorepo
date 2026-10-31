using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FirestoreMigration;

internal static class RecipeTimestampParser
{
    public static DateTime Parse(JsonElement timestamp, string path)
    {
        long seconds;
        long nanoseconds;
        if (timestamp.TryGetProperty("_seconds", out var legacySeconds))
        {
            seconds = Integer(legacySeconds, $"{path}._seconds");
            if (!timestamp.TryGetProperty("_nanoseconds", out var legacyNanoseconds))
                throw new FormatException($"{path}._nanoseconds: required timestamp field is missing.");
            nanoseconds = Integer(legacyNanoseconds, $"{path}._nanoseconds");
        }
        else
        {
            if (!timestamp.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                type.GetString() != "firestore/timestamp/1.0")
                throw new FormatException($"{path}.type: unsupported timestamp type.");
            if (!timestamp.TryGetProperty("seconds", out var standardSeconds) ||
                !timestamp.TryGetProperty("nanoseconds", out var standardNanoseconds))
                throw new FormatException($"{path}: timestamp seconds and nanoseconds are required.");
            seconds = Integer(standardSeconds, $"{path}.seconds");
            nanoseconds = Integer(standardNanoseconds, $"{path}.nanoseconds");
        }
        if (nanoseconds is < 0 or > 999_999_999)
            throw new FormatException($"{path}: nanoseconds must be between 0 and 999999999.");
        if (nanoseconds % 1_000 != 0)
            throw new FormatException($"{path}: timestamp exceeds PostgreSQL microsecond precision.");

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanoseconds / 100).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException($"{path}: timestamp is outside the supported UTC range.", exception);
        }
    }

    private static long Integer(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
            throw new FormatException($"{path}: expected an integer.");
        return number;
    }
}

internal static class RecipeSourceFingerprint
{
    public static string Create(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
