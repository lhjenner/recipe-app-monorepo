using System.Security.Cryptography;
using System.Text.Json;
using FirestoreMigration;
using FluentAssertions;
using RecipeApp.TestFixtures;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class RecipeImageManifestTests
{
    private const string RecipeId = "synthetic-recipe";
    private const string ImageReference = "https://example.test/synthetic";

    [Fact]
    public void Validates_only_the_explicit_manifest_file_and_detects_format_from_content()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "synthetic.jpg"), SyntheticImage.Png);
        File.WriteAllText(Path.Combine(directory.Path, "unrelated.bin"), "not opened");
        var entry = Entry("synthetic.jpg", SyntheticImage.Png, ImageReference);

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)],
            [entry],
            directory.Path);

        result.BlockingDiagnostics.Should().BeEmpty();
        result.Images.Should().ContainSingle().Which.Should().Match<RecipeImageValidationResult>(image =>
            image.Status == "validated" &&
            image.ContentType == "image/png" &&
            image.Width == 1 &&
            image.Height == 1 &&
            image.Sha256 == Convert.ToHexString(SHA256.HashData(SyntheticImage.Png)));
    }

    [Fact]
    public void Blocks_referenced_images_without_a_manifest()
    {
        var result = RecipeImageManifestValidator.Validate([SourceRecipe(ImageReference)], null, null);

        result.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("explicit image manifest");
        result.Images.Should().ContainSingle().Which.Status.Should().Be("invalid");
    }

    [Fact]
    public void Requires_exact_recipe_and_image_reference_mapping()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "synthetic.png"), SyntheticImage.Png);
        var entry = Entry("synthetic.png", SyntheticImage.Png, "https://example.test/other");

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)],
            [entry],
            directory.Path);

        result.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("does not exactly match");
        result.Images.Should().ContainSingle().Which.Status.Should().Be("invalid");
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("C:\\outside.png")]
    [InlineData("/outside.png")]
    [InlineData("images/../outside.png")]
    public void Rejects_rooted_and_traversal_paths(string relativePath)
    {
        using var directory = new TemporaryDirectory();
        var entry = Entry(relativePath, SyntheticImage.Png, ImageReference);

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)],
            [entry],
            directory.Path);

        result.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("path");
        result.Images.Should().ContainSingle().Which.Status.Should().Be("invalid");
    }

    [Fact]
    public void Blocks_checksum_and_byte_count_mismatches_before_decoding()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "synthetic.png"), SyntheticImage.Png);
        var checksumMismatch = Entry("synthetic.png", SyntheticImage.Png, ImageReference) with
        {
            Sha256 = new string('0', 64)
        };
        var mismatch = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)], [checksumMismatch], directory.Path);
        mismatch.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("SHA-256");

        var byteCountMismatch = Entry("synthetic.png", SyntheticImage.Png, ImageReference) with
        {
            ByteCount = SyntheticImage.Png.Length + 1
        };
        var count = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)], [byteCountMismatch], directory.Path);
        count.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("byte count");
    }

    [Fact]
    public void Rejects_unsupported_and_corrupt_image_bytes_even_with_valid_checksum()
    {
        using var directory = new TemporaryDirectory();
        var corrupt = "not an image"u8.ToArray();
        File.WriteAllBytes(Path.Combine(directory.Path, "synthetic.png"), corrupt);
        var entry = Entry("synthetic.png", corrupt, ImageReference);

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)], [entry], directory.Path);

        result.BlockingDiagnostics.Should().ContainSingle();
        result.Images.Should().ContainSingle().Which.Status.Should().Be("invalid");
    }

    [Fact]
    public void Enforces_configured_file_size_limit_before_reading_contents()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = Path.Combine(directory.Path, "oversized.png");
        using (var stream = File.Create(imagePath))
            stream.SetLength(RecipeImageManifestValidator.MaximumBytes + 1);
        var entry = new RecipeImageManifestEntry(RecipeId, ImageReference, "oversized.png",
            RecipeImageManifestValidator.MaximumBytes + 1, new string('0', 64));

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(ImageReference)], [entry], directory.Path);

        result.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("size must be between");
    }

    [Fact]
    public void Parser_rejects_duplicate_recipe_entries_and_invalid_manifest_data()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var duplicate = JsonSerializer.Serialize(new[]
        {
            ToManifestJson(Entry("a.png", [1], ImageReference)),
            ToManifestJson(Entry("b.png", [2], ImageReference))
        }, jsonOptions);
        var parseDuplicate = () => RecipeImageManifestParser.Parse(duplicate);
        parseDuplicate.Should().Throw<FormatException>().WithMessage("*duplicate Recipe image manifest entry*");

        var badHash = JsonSerializer.Serialize(new[]
        {
            ToManifestJson(Entry("a.png", [1], ImageReference) with { Sha256 = "bad" })
        }, jsonOptions);
        var parseHash = () => RecipeImageManifestParser.Parse(badHash);
        parseHash.Should().Throw<FormatException>().WithMessage("*.sha256*");
    }

    [Fact]
    public void Ignores_unreferenced_manifest_entries_without_opening_their_files()
    {
        using var directory = new TemporaryDirectory();
        var orphan = Entry("does-not-exist.png", [1], "https://example.test/unreferenced") with
        {
            RecipeId = "unreferenced-recipe"
        };

        var result = RecipeImageManifestValidator.Validate(
            [SourceRecipe(null)], [orphan], directory.Path);

        result.BlockingDiagnostics.Should().BeEmpty();
        result.Warnings.Should().ContainSingle().Which.Should().Contain("file was not opened");
    }

    private static RecipeImageManifestEntry Entry(string path, byte[] bytes, string imageReference) =>
        new(RecipeId, imageReference, path, bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)));

    private static object ToManifestJson(RecipeImageManifestEntry entry) => new
    {
        recipeId = entry.RecipeId,
        imageReference = entry.ImageReference,
        path = entry.RelativePath,
        byteCount = entry.ByteCount,
        sha256 = entry.Sha256
    };

    private static RecipeSourceRecord SourceRecipe(string? imageReference) => new(
        RecipeId,
        "Synthetic recipe",
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        false,
        null,
        null,
        imageReference,
        """{"id":"synthetic-recipe","title":"Synthetic recipe"}""");

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"recipe-image-validation-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
