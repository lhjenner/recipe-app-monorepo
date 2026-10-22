using System.Text.Json;

namespace FirestoreMigration;

internal static class ProductParser
{
    public static List<LinkValue> Read(JsonElement ingredient, string name, MigrationSource source)
    {
        var result = new List<LinkValue>();
        var preferred = JsonInput.OptionalText(ingredient, "preferredVariant", 200);
        if (ingredient.TryGetProperty("productUrls", out var products))
        {
            JsonInput.Require(products, JsonValueKind.Object, "productUrls");
            foreach (var chain in products.EnumerateObject())
            {
                source.ProductChainCodes.Add(chain.Name);
                JsonInput.Require(chain.Value, JsonValueKind.Array, $"productUrls.{chain.Name}");
                var position = 0;
                foreach (var link in chain.Value.EnumerateArray())
                {
                    JsonInput.Require(link, JsonValueKind.Object, "product link");
                    var url = JsonInput.Text(link, "url", true, 2048);
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                        source.Errors.Add($"{name}: product URL must be absolute HTTPS: {JsonSerializer.Serialize(url)}.");
                    result.Add(new(chain.Name, position++, url,
                        JsonInput.OptionalText(link, "variantLabel", 200),
                        JsonInput.OptionalText(link, "note", 1000),
                        JsonInput.Flag(link, "supportsBothPricingMethods"), false));
                    JsonInput.Ignored(link, "ingredients.productUrls.link", source,
                        "url", "variantLabel", "note", "supportsBothPricingMethods");
                }
            }
        }
        if (preferred is null) return result;
        var matches = result.Where(link => link.VariantLabel == preferred).ToArray();
        if (matches.Length == 0)
            source.Warnings.Add($"{name}: preferredVariant {JsonSerializer.Serialize(preferred)} matches no link.");
        foreach (var chain in matches.GroupBy(link => link.Chain))
        {
            if (chain.Count() != 1)
            {
                source.Warnings.Add($"{name}: preferredVariant is ambiguous for chain '{chain.Key}'; no preferred link.");
                continue;
            }
            var link = chain.Single();
            result[result.IndexOf(link)] = link with { IsPreferred = true };
        }
        return result;
    }
}
