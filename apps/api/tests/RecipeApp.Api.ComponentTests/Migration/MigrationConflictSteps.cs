using System.Text.Json;
using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Infrastructure.Persistence;
using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Migration;

[Binding]
public sealed class MigrationConflictSteps(MigrationFixture fixture)
{
    [When("the (.*) changes during ingredient approval")]
    public async Task ChangeDuringApprovalAsync(string resource)
    {
        fixture.ReplaceSource();
        await fixture.CaptureFailureAsync(async () => await fixture.RunAsync(true, onApproval: async () =>
        {
            if (resource == "source") { File.AppendAllText(fixture.BackupFile, " "); return; }
            if (resource == "decisions") { File.AppendAllText(fixture.DecisionFile, " "); return; }
            await using var db = fixture.Database();
            switch (resource)
            {
                case "ingredient":
                    (await db.Ingredients.SingleAsync()).NutritionSource = "concurrent";
                    break;
                case "link":
                    (await db.ProductLinks.SingleAsync()).Note = "concurrent";
                    break;
                case "mapping":
                    (await db.IngredientStoreMappings.SingleAsync(mapping => !mapping.IsNotStocked)).DepartmentId = null;
                    (await db.IngredientStoreMappings.SingleAsync(mapping => !mapping.IsNotStocked)).IsNotStocked = true;
                    break;
                case "lookup":
                    (await db.Departments.SingleAsync()).Name = "Concurrent aisle";
                    break;
                case "insertion":
                    db.Categories.Add(new() { Id = Guid.NewGuid(), OwnerUserId = fixture.Owner,
                        Name = "Concurrent", NormalizedName = "concurrent" });
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(resource));
            }
            await db.SaveChangesAsync();
        }));
        fixture.Failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("changed after preview");
    }

    [When("I attempt a migration with (.*)")]
    public async Task InvalidAsync(string problem)
    {
        if (problem is "ownership conflict" or "archived name conflict" or "incompatible heading")
        {
            await fixture.RunAsync(true);
            await using var db = fixture.Database();
            var row = await db.Ingredients.SingleAsync();
            if (problem == "ownership conflict")
            {
                var other = Guid.NewGuid();
                db.Users.Add(new User { Id = other, Email = $"{other:N}@example.test",
                    PasswordHash = Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTime.UtcNow });
                row.OwnerUserId = other;
            }
            else row.ArchivedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            fixture.Before = await fixture.ReadAsync();
        }
        var json = fixture.OriginalJson();
        switch (problem)
        {
            case "duplicate IDs":
                using (var document = JsonDocument.Parse(json))
                    json = $"[{document.RootElement[0].GetRawText()},{document.RootElement[0].GetRawText()}]";
                break;
            case "archived name conflict":
                json = json.Replace(fixture.IngredientId.ToString(), Guid.NewGuid().ToString())
                    .Replace("Synthetic syrup", " SYNTHETIC   SYRUP ");
                break;
            case "unknown store": json = json.Replace("woolworths-carlyle", "unknown-store"); break;
            case "unknown chain": json = json.Replace("\"woolworths\"", "\"unknown-chain\""); break;
            case "unclassified invalid": json = json.Replace("\"name\":", "\"invalid\":true,\"name\":"); break;
            case "incompatible heading":
                json = json.Replace("\"name\":", "\"invalid\":true,\"name\":");
                File.WriteAllText(fixture.DecisionFile, JsonSerializer.Serialize(new Dictionary<Guid, object>
                {
                    [fixture.IngredientId] = new { name = "Synthetic syrup", classification = "Heading" }
                }));
                break;
        }
        File.WriteAllText(fixture.BackupFile, json);
        fixture.Result = await fixture.RunAsync(true);
    }

    [Then("blocking migration diagnostics leave the target unchanged")]
    public async Task BlockedAsync()
    {
        fixture.Result!.Errors.Should().BeGreaterThan(0);
        fixture.Result.Inserted.Should().Be(0);
        fixture.Result.Updated.Should().Be(0);
        await using var db = fixture.Database();
        (await db.Ingredients.CountAsync()).Should().Be(fixture.Before is null ? 0 : 1);
        if (fixture.Before is not null) (await fixture.ReadAsync()).Version.Should().Be(fixture.Before.Version);
        else
        {
            (await db.Categories.CountAsync()).Should().Be(0);
            (await db.Departments.CountAsync()).Should().Be(0);
        }
        if (fixture.Before?.OwnerUserId != fixture.Owner)
            fixture.Output.ToString().Should().NotContain("PasswordHash");
    }

    [When("I import explicitly classified invalid items")]
    public async Task ClassificationsAsync()
    {
        var heading = Guid.NewGuid();
        var note = Guid.NewGuid();
        File.WriteAllText(fixture.BackupFile, JsonSerializer.Serialize(new[]
        {
            new { id = fixture.IngredientId, name = "Water", invalid = true },
            new { id = heading, name = "Optional:", invalid = true },
            new { id = note, name = "To taste", invalid = true }
        }));
        fixture.DecisionContents = JsonSerializer.Serialize(new Dictionary<Guid, object>
        {
            [fixture.IngredientId] = new { name = "Water", classification = "Ingredient" },
            [heading] = new { name = "Optional:", classification = "Heading" },
            [note] = new { name = "To taste", classification = "Note" },
            [Guid.NewGuid()] = new { name = "Absent retained decision", classification = "Note" }
        });
        File.WriteAllText(fixture.DecisionFile, fixture.DecisionContents);
        fixture.Result = await fixture.RunAsync(true);
    }

    [Then("only the excluded ingredient is created and decisions are unchanged")]
    public async Task ClassifiedAsync()
    {
        var row = await fixture.ReadAsync();
        row.ExcludeFromGroceryList.Should().BeTrue();
        row.Name.Should().Be("Water");
        await using var db = fixture.Database();
        (await db.Ingredients.CountAsync()).Should().Be(1);
        fixture.Result!.Headings.Should().Be(1);
        fixture.Result.Notes.Should().Be(1);
        File.ReadAllText(fixture.DecisionFile).Should().Be(fixture.DecisionContents);
    }
}
