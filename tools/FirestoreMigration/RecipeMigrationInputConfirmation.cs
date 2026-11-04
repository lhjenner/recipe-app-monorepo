namespace FirestoreMigration;

public static class RecipeMigrationInputConfirmation
{
    public static async Task ConfirmAsync(RecipeMigrationSource source, MigrationOptions options,
        string recipePath, string decisionPath, string? imageManifestPath,
        TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        var sourceOwners = source.LegacyOwnerIds.Order(StringComparer.Ordinal).ToArray();
        var selectedOwners = options.SelectedLegacyOwnerIds?.Order(StringComparer.Ordinal).ToArray();
        output.WriteLine($"Recipe source: {recipePath}");
        output.WriteLine($"Decision file: {decisionPath}");
        output.WriteLine($"Destination owner: {options.Owner}");
        output.WriteLine(selectedOwners is null
            ? $"Source-owner selection: all ({string.Join(", ", sourceOwners)}); missing owner fields: {source.OwnerFieldCounts.Missing}"
            : $"Source-owner selection: {string.Join(", ", selectedOwners)}; missing owner fields: {source.OwnerFieldCounts.Missing}");
        output.WriteLine(imageManifestPath is null
            ? "Image inputs: none selected."
            : $"Image manifest: {imageManifestPath}; image root: {options.ImageRoot}");
        output.Write("Type the destination owner GUID to confirm these inputs: ");

        var response = await input.ReadLineAsync(cancellationToken);
        if (!Guid.TryParse(response, out var confirmedOwner) || confirmedOwner != options.Owner)
            throw new InvalidOperationException(
                "Destination owner confirmation did not match; the Recipe preview was not run.");
    }
}
