using System.Text.Json;

namespace FirestoreMigration;

internal static class JsonInput
{
    public static JsonDocument Read(string json, JsonValueKind kind, string context)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            try
            {
                Require(document.RootElement, kind, context);
                CheckDuplicates(document.RootElement, context);
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }
        catch (JsonException exception)
        {
            throw new FormatException($"{context}: malformed JSON at line {exception.LineNumber}.", exception);
        }
    }

    public static void Require(JsonElement value, JsonValueKind kind, string context)
    {
        if (value.ValueKind != kind) throw new FormatException($"{context}: expected {kind}.");
    }

    public static string Text(JsonElement element, string key, bool required = false, int? maximum = null)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required) throw new FormatException($"Missing required '{key}'.");
            return "";
        }
        Require(value, JsonValueKind.String, key);
        var text = value.GetString()!;
        if (required && string.IsNullOrWhiteSpace(text))
            throw new FormatException($"'{key}' must not be blank.");
        if (maximum.HasValue && text.Length > maximum)
            throw new FormatException($"'{key}' exceeds {maximum} characters.");
        return text;
    }

    public static string? OptionalText(JsonElement element, string key, int? maximum = null)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return Text(element, key, maximum: maximum);
    }

    public static bool Flag(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value)) return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new FormatException($"'{key}' must be a boolean.");
        return value.GetBoolean();
    }

    public static void Ignored(JsonElement element, string path, MigrationSource source, params string[] known)
    {
        foreach (var property in element.EnumerateObject().Where(property => !known.Contains(property.Name)))
        {
            var key = $"{path}.{property.Name}";
            source.Ignored[key] = source.Ignored.GetValueOrDefault(key) + 1;
        }
    }

    private static void CheckDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new FormatException($"{path}: duplicate key '{property.Name}'.");
                CheckDuplicates(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in element.EnumerateArray()) CheckDuplicates(value, path);
        }
    }
}
