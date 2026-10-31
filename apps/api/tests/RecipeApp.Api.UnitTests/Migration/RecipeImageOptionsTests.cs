using FirestoreMigration;
using FluentAssertions;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class RecipeImageOptionsTests
{
    private const string Owner = "117c8374-5db4-4021-b106-5b466e2446ed";

    [Fact]
    public void RecipeMode_AcceptsExplicitJournalPath()
    {
        var options = MigrationOptions.Parse([
            "--mode", "recipes", "--recipes", "recipes.json", "--owner", Owner,
            "--decisions", "decisions.json", "--report", "report.json", "--write",
            "--image-manifest", "manifest.json", "--image-root", "images", "--image-journal", "journal.json"
        ]);
        options.ImageJournalFile.Should().Be(Path.GetFullPath("journal.json"));
    }

    [Fact]
    public void IngredientMode_RejectsImageJournal()
    {
        var parse = () => MigrationOptions.Parse([
            "--backup", "backup", "--owner", Owner, "--decisions", "decisions.json",
            "--image-journal", "journal.json"
        ]);
        parse.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RecipeMode_RejectsBlankJournalPath(string journal)
    {
        var parse = () => MigrationOptions.Parse([
            "--mode", "recipes", "--recipes", "recipes.json", "--owner", Owner,
            "--decisions", "decisions.json", "--report", "report.json", "--image-journal", journal
        ]);
        parse.Should().Throw<FormatException>();
    }
}
