using System.IO.Compression;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeBackup;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace RecipeApp.Api.ComponentTests.Backup;

public sealed class BackupRehearsalTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _source = Database();
    private readonly PostgreSqlContainer _target = Database();
    private readonly MemoryStorage _sourceStorage = new();
    private readonly MemoryStorage _targetStorage = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"recipe-rehearsal-{Guid.NewGuid():N}");
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _recipe = Guid.NewGuid();
    private const string Key = "synthetic/opaque image.png";
    private string Archive => Path.Combine(_directory, "recovery.zip");
    private BackupRunner Exporter => new(new PostgresTools(_source.Id));
    private BackupRunner Restorer => new(new PostgresTools(_target.Id));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        await Task.WhenAll(_source.StartAsync(), _target.StartAsync());
        await using var database = Context(_source);
        await database.Database.MigrateAsync();
        database.Users.Add(new User
        {
            Id = _owner, Email = "synthetic-backup@example.invalid", PasswordHash = "not-a-login-hash",
            CreatedAtUtc = DateTime.UtcNow
        });
        database.Recipes.Add(new Recipe
        {
            Id = _recipe, OwnerUserId = _owner, Name = "Synthetic Archived Recipe",
            NormalizedName = "synthetic archived recipe", IsArchived = true,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            Image = new RecipeImage
            {
                RecipeId = _recipe, ObjectKey = Key, ContentType = "image/png",
                ByteSize = TestFixtures.SyntheticImage.Png.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(TestFixtures.SyntheticImage.Png)).ToLowerInvariant(),
                Width = 1, Height = 1
            }
        });
        await database.SaveChangesAsync();
        _sourceStorage.Objects[Key] = TestFixtures.SyntheticImage.Png;
    }

    [Fact]
    public async Task FullRehearsal_RestoresDatabaseAndExactImageBytes_ThenRefusesNonemptyTargets()
    {
        var manifest = await Exporter.ExportAsync(_source.GetConnectionString(), _sourceStorage, Archive);
        output.WriteLine($"Synthetic archive: {new FileInfo(Archive).Length} bytes; database dump: {manifest.DatabaseBytes} bytes.");
        manifest.Images.Should().ContainSingle().Which.OwnerUserId.Should().Be(_owner);
        var recoveredArchive = await ArchiveEncryption.RecoverAsync(Archive, _directory);
        using var client = new EmptyBucketClient();

        await Restorer.RestoreAsync(_target.GetConnectionString(), client, "isolated-test",
            _targetStorage, recoveredArchive);

        await using var restored = Context(_target);
        (await restored.Users.SingleAsync()).Email.Should().Be("synthetic-backup@example.invalid");
        var recipe = await restored.Recipes.Include(row => row.Image).SingleAsync();
        recipe.Id.Should().Be(_recipe);
        recipe.IsArchived.Should().BeTrue();
        recipe.Name.Should().Be("Synthetic Archived Recipe");
        recipe.Image!.ObjectKey.Should().Be(Key);
        _targetStorage.Objects[Key].Should().Equal(TestFixtures.SyntheticImage.Png);
        (await restored.Database.GetAppliedMigrationsAsync()).Should().HaveCount(6);

        var repeat = () => Restorer.RestoreAsync(_target.GetConnectionString(), client,
            "isolated-test", _targetStorage, Archive);
        await repeat.Should().ThrowAsync<InvalidOperationException>().WithMessage("*empty database*");
    }

    [Fact]
    public async Task Restore_RejectsNonemptyBucket_WithoutRestoringDatabase()
    {
        await Exporter.ExportAsync(_source.GetConnectionString(), _sourceStorage, Archive);
        using var client = new EmptyBucketClient { Nonempty = true };
        var act = () => Restorer.RestoreAsync(_target.GetConnectionString(), client,
            "isolated-test", _targetStorage, Archive);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*empty destination bucket*");
        _targetStorage.Objects.Should().BeEmpty();
        await using var destination = Context(_target);
        (await destination.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Dump_UsesExportedSnapshot_NotLaterImageAssociationChanges()
    {
        await using var connection = new NpgsqlConnection(_source.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await using var command = new NpgsqlCommand("SELECT pg_export_snapshot()", connection, transaction);
        var snapshot = (string)(await command.ExecuteScalarAsync())!;
        await using var source = Context(_source);
        var image = await source.RecipeImages.SingleAsync();
        var manifestImage = new BackupImage(_recipe, _owner, image.ObjectKey, image.ContentType,
            image.ByteSize, image.Sha256, image.Width, image.Height);
        image.ObjectKey = "newer/object.png";
        await source.SaveChangesAsync();
        var dump = Path.Combine(_directory, "snapshot.dump");
        await new PostgresTools(_source.Id).DumpAsync(_source.GetConnectionString(), snapshot, dump, CancellationToken.None);
        await transaction.CommitAsync();
        await BackupArchive.CreateAsync(Archive, dump, [manifestImage], _sourceStorage, CancellationToken.None);

        using var client = new EmptyBucketClient();
        await Restorer.RestoreAsync(_target.GetConnectionString(), client, "isolated-test", _targetStorage, Archive);

        await using var target = Context(_target);
        (await target.RecipeImages.SingleAsync()).ObjectKey.Should().Be(Key);
        _targetStorage.Objects.Keys.Should().Equal(Key);
    }

    [Fact]
    public async Task Export_MissingObjectPublishesNothing_AndTamperedRestoreMakesNoWrites()
    {
        _sourceStorage.Objects.Clear();
        var act = () => Exporter.ExportAsync(_source.GetConnectionString(), _sourceStorage, Archive);
        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Archive).Should().BeFalse();
        _sourceStorage.Objects[Key] = TestFixtures.SyntheticImage.Png;
        await Exporter.ExportAsync(_source.GetConnectionString(), _sourceStorage, Archive);
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Update))
        {
            zip.GetEntry("database.dump")!.Delete();
            await using var corrupted = zip.CreateEntry("database.dump").Open();
            await corrupted.WriteAsync("corrupt"u8.ToArray());
        }
        using var client = new EmptyBucketClient();
        var restore = () => Restorer.RestoreAsync(_target.GetConnectionString(), client,
            "isolated-test", _targetStorage, Archive);
        await restore.Should().ThrowAsync<InvalidDataException>();
        client.ListCalls.Should().Be(0);
        _targetStorage.Objects.Should().BeEmpty();
        await using var destination = Context(_target);
        (await destination.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_source.DisposeAsync().AsTask(), _target.DisposeAsync().AsTask());
        foreach (var path in Directory.GetFiles(_directory)) File.Delete(path);
        Directory.Delete(_directory);
    }

    private static PostgreSqlContainer Database() => new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("synthetic_recovery").WithUsername("postgres")
        .WithPassword(Guid.NewGuid().ToString("N")).Build();

    private static AppDbContext Context(PostgreSqlContainer container) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(container.GetConnectionString()).Options);

    private sealed class EmptyBucketClient() : AmazonS3Client(new AnonymousAWSCredentials(),
        new AmazonS3Config { ServiceURL = "http://localhost", ForcePathStyle = true })
    {
        public bool Nonempty { get; init; }
        public int ListCalls { get; private set; }
        public override Task<ListObjectsV2Response> ListObjectsV2Async(
            ListObjectsV2Request request, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(new ListObjectsV2Response
            {
                KeyCount = Nonempty ? 1 : 0, IsTruncated = false,
                S3Objects = Nonempty ? [new S3Object { Key = "existing" }] : []
            });
        }
    }

    private sealed class MemoryStorage : IPrivateObjectStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Objects.TryGetValue(objectKey, out var bytes) ? new MemoryStream(bytes) : null);
        public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
        {
            content.CanSeek.Should().BeTrue("the actual S3 adapter needs a known stream length");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Objects[objectKey] = buffer.ToArray();
        }
    }
}
