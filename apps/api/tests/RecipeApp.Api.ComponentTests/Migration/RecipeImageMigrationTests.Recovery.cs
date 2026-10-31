using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class RecipeImageMigrationTests
{
    [Fact]
    public async Task AC34_ArchivedSource_StillGetsVerifiedImageAssociation()
    {
        var source = RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true)
            .Replace("\"title\":", "\"archived\":true,\"title\":", StringComparison.Ordinal);
        var result = await _fixture.RunRecipeAsync(source, true, _fixture.NewRecipeReportPath(),
            includeImageManifest: true);
        result.Status.Should().Be("committed");
        await using var db = _fixture.Database();
        var recipe = await db.Recipes.Include(row => row.Image).SingleAsync();
        recipe.IsArchived.Should().BeTrue();
        _storage.Objects[recipe.Image!.ObjectKey].Should().Equal(TestFixtures.SyntheticImage.Png);
    }

    [Fact]
    public async Task AC36_CleanupAgainstWrongDatabase_IsRejectedBeforeAnyObjectOperation()
    {
        _storage.FailAfterWrite = true;
        var attempt = () => ImportAsync();
        await attempt.Should().ThrowAsync<IOException>();
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!);
        var connection = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = "different_database" };
        await using var wrongDatabase = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        var cleanup = () => RecipeImageRecovery.RecoverAsync(wrongDatabase, _storage, journal, true);
        await cleanup.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match*");
        _storage.Deletes.Should().Be(0);
    }

    [Fact]
    public async Task AC35_UnchangedAssociationWithCorruptStoredBytes_BlocksRepeat()
    {
        await ImportAsync();
        _storage.Objects[await ImageKeyAsync()] = [1, 2, 3];
        var repeat = () => ImportAsync();
        await repeat.Should().ThrowAsync<InvalidDataException>();
        _storage.Creates.Should().Be(1);
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AC16_ImageBytesChangedDuringApproval_BlocksBeforeUpload()
    {
        await ImportAsync();
        var original = await ImageKeyAsync();
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true, title: "Changed metadata"),
            true, _fixture.NewRecipeReportPath(), includeImageManifest: true,
            onUpdateApproval: () =>
            {
                File.WriteAllBytes(Path.Combine(_fixture.ImageRoot, "synthetic.png"), [1, 2, 3]);
                return Task.CompletedTask;
            });
        await attempt.Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed after preview*");
        (await ImageKeyAsync()).Should().Be(original);
        _storage.Creates.Should().Be(1);
    }

    [Fact]
    public async Task AC35_InvalidSelectedImage_BlocksBeforeJournalOrUpload()
    {
        var result = await _fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, _fixture.NewRecipeReportPath(), includeImageManifest: true, corruptImage: true);
        result.Status.Should().Be("validation-blocked");
        _storage.Creates.Should().Be(0);
        File.Exists(_fixture.ImageJournalPath).Should().BeFalse();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }
}
