using System.Security.Cryptography;
using System.Text.Json;
using FirestoreMigration;
using FluentAssertions;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class RecipeImageJournalTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"image-journal-test-{Guid.NewGuid():N}.json");
    private readonly Guid _owner = Guid.NewGuid();
    private const string Scope = "synthetic-private";
    private static string Fingerprint => new('A', 64);
    private static string Database => new('B', 64);
    private static RecipeImageValidationResult Image => new("legacy", "not-fetched", "synthetic.png",
        "image/png", 3, Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })), 1, 1, "validated", null);

    [Fact]
    public void PlannedKey_IsDurableBeforeUpload_AndReusedAfterReopen()
    {
        JournalImage planned;
        Guid run;
        using (var journal = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint))
        {
            planned = journal.Plan(Image);
            run = journal.Data.RunId;
            planned.State.Should().Be("planned");
            JsonSerializer.Deserialize<ImageJournalData>(File.ReadAllBytes(_path))!.Images
                .Should().ContainSingle().Which.Should().Be(planned);
        }
        using var retry = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint);
        retry.Data.RunId.Should().Be(run);
        retry.Plan(Image).Should().Be(planned);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("storage")]
    [InlineData("database")]
    [InlineData("input")]
    public void Journal_CannotBeReusedForDifferentTargetsOrInputs(string changed)
    {
        using (RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint)) { }
        var attempt = () => RecipeImageJournal.Open(_path,
            changed == "owner" ? Guid.NewGuid() : _owner,
            changed == "storage" ? "other" : Scope,
            changed == "database" ? new string('C', 64) : Database,
            changed == "input" ? new string('D', 64) : Fingerprint);
        attempt.Should().Throw<InvalidOperationException>();
        using var original = RecipeImageJournal.OpenExisting(_path);
        original.Data.OwnerId.Should().Be(_owner);
    }

    [Fact]
    public void TamperedKey_IsRejectedBeforeRecoveryCanUseIt()
    {
        ImageJournalData data;
        using (var journal = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint))
        {
            journal.Plan(Image);
            data = journal.Data;
        }
        File.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(data with
        {
            Images = [data.Images.Single() with { ObjectKey = "foreign/referenced-object" }]
        }));
        var load = () => RecipeImageJournal.OpenExisting(_path);
        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void JournalLock_PreventsConcurrentRetryOrCleanup()
    {
        using var journal = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint);
        var simultaneous = () => RecipeImageJournal.OpenExisting(_path);
        simultaneous.Should().Throw<IOException>();
    }

    [Fact]
    public void DeletedJournalEntry_IsNotSilentlyUploadedAgain()
    {
        using var journal = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint);
        var image = journal.Plan(Image);
        journal.SetState(image.ObjectKey, "deleted");
        var reuse = () => journal.Plan(Image);
        reuse.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void MissingHeaderFields_AreRejectedInsteadOfUsingDefaults()
    {
        File.WriteAllText(_path, """{"SchemaVersion":1}""");
        var load = () => RecipeImageJournal.OpenExisting(_path);
        load.Should().Throw<JsonException>();
    }

    [Fact]
    public void StateChangeForNonjournaledKey_IsRejected()
    {
        using var journal = RecipeImageJournal.Open(_path, _owner, Scope, Database, Fingerprint);
        var change = () => journal.SetState("unrecorded", "deleted");
        change.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        File.Delete(_path);
        File.Delete(_path + ".lock");
    }
}
