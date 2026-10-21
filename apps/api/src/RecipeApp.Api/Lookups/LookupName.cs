namespace RecipeApp.Api.Lookups;

internal static class LookupName
{
    public static string Clean(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string value) => Clean(value).ToLowerInvariant();
}
