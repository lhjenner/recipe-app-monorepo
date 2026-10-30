using System.IO.Compression;
using System.Security.Cryptography;
using FluentAssertions;
using RecipeApp.Api.Images;
using RecipeBackup;

namespace RecipeApp.Api.UnitTests.Backup;

public sealed class BackupArchiveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"recipe-backup-unit-{Guid.NewGuid():N}");
    private readonly byte[] _image = TestFixtures.SyntheticImage.Png;

    public BackupArchiveTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task RoundTrip_PreservesOpaqueKeysAndAssociationsWithoutUsingKeysAsFilePaths()
    {
        var image = Image("../../opaque/key");
        var path = await CreateAsync([image, image with { RecipeId = Guid.NewGuid() }]);

        var manifest = await BackupArchive.VerifyAsync(path, CancellationToken.None);
        manifest.Images.Should().HaveCount(2);
        manifest.Images[0].Should().Be(image);
        using var zip = ZipFile.OpenRead(path);
        zip.Entries.Count(entry => entry.FullName.StartsWith("objects/")).Should().Be(1);
        zip.Entries.Should().OnlyContain(entry => !entry.FullName.Contains(".."));
    }

    [Fact]
    public async Task Export_CorruptOrMissingImagePublishesNoArchive()
    {
        var output = Path.Combine(_directory, "invalid.zip");
        var dump = await DumpAsync();
        var store = new TestStorage { Content = [1, 2, 3] };

        var act = () => BackupArchive.CreateAsync(output, dump, [Image("key")], store, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(output).Should().BeFalse();
        File.Exists(output + ".partial").Should().BeFalse();
        store.Content = null;
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("database.dump")]
    [InlineData("object")]
    [InlineData("manifest")]
    [InlineData("unexpected")]
    [InlineData("duplicate")]
    public async Task Verify_RejectsTamperingAndAmbiguousEntries(string change)
    {
        var path = await CreateAsync([Image("key")]);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var name = change switch
            {
                "object" => zip.Entries.Single(entry => entry.FullName.StartsWith("objects/")).FullName,
                "manifest" => "manifest.json",
                "unexpected" => "../outside",
                "duplicate" => "database.dump",
                _ => change
            };
            if (change is not ("unexpected" or "duplicate")) zip.GetEntry(name)!.Delete();
            await using var target = zip.CreateEntry(name).Open();
            await target.WriteAsync("corrupt"u8.ToArray());
        }

        var act = () => BackupArchive.VerifyAsync(path, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Export_NeverOverwritesAnExistingBackup()
    {
        var path = await CreateAsync([]);
        var before = await File.ReadAllBytesAsync(path);
        var dump = await DumpAsync();

        var act = () => BackupArchive.CreateAsync(path, dump, [], new TestStorage(), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await File.ReadAllBytesAsync(path)).Should().Equal(before);
    }

    [Fact]
    public async Task Export_CancellationDoesNotPublishAnArchive()
    {
        var output = Path.Combine(_directory, "cancelled.zip");
        var dump = await DumpAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => BackupArchive.CreateAsync(output, dump, [Image("key")],
            new TestStorage { Content = _image }, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public async Task Export_DeduplicatedContentStillChecksEveryReferencedObjectKey()
    {
        var output = Path.Combine(_directory, "missing-shared.zip");
        var image = Image("present");
        var dump = await DumpAsync();
        var act = () => BackupArchive.CreateAsync(output, dump,
            [image, image with { RecipeId = Guid.NewGuid(), ObjectKey = "missing" }],
            new TestStorage { Content = _image, MissingKey = "missing" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public async Task Export_DoesNotRemoveAnotherProcessesPartialFile()
    {
        var output = Path.Combine(_directory, "already-running.zip");
        await File.WriteAllTextAsync(output + ".partial", "owned by another export");
        var dump = await DumpAsync();
        var act = () => BackupArchive.CreateAsync(output, dump, [], new TestStorage(), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await File.ReadAllTextAsync(output + ".partial")).Should().Be("owned by another export");
    }

    private BackupImage Image(string key) => new(Guid.NewGuid(), Guid.NewGuid(), key, "image/png",
        _image.Length, Convert.ToHexString(SHA256.HashData(_image)).ToLowerInvariant(), 1, 1);

    private async Task<string> CreateAsync(IReadOnlyList<BackupImage> images) =>
        await BackupArchive.CreateAsync(Path.Combine(_directory, $"{Guid.NewGuid():N}.zip"),
            await DumpAsync(), images, new TestStorage { Content = _image }, CancellationToken.None);

    private async Task<string> DumpAsync()
    {
        var path = Path.Combine(_directory, "synthetic.dump");
        await File.WriteAllBytesAsync(path, "Synthetic dump; real pg_dump covered by component rehearsal."u8.ToArray());
        return path;
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }

    private sealed class TestStorage : IPrivateObjectStorage
    {
        public byte[]? Content { get; set; }
        public string? MissingKey { get; init; }
        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream?>(Content is null || objectKey == MissingKey
                ? null : new MemoryStream(Content, writable: false));
        }

        public Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
