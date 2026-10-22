using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Migration;

[Binding]
public sealed class MigrationSteps(MigrationFixture fixture)
{
    [Given("a synthetic Firestore backup and an existing migration owner")]
    public Task InitializeAsync() => fixture.InitializeAsync();

    [When("I preview the synthetic migration")]
    public async Task PreviewAsync() => fixture.Result = await fixture.RunAsync(false);

    [Then("the migration database has no ingredients or lookups")]
    public async Task EmptyAsync()
    {
        await using var db = fixture.Database();
        (await db.Ingredients.CountAsync()).Should().Be(0);
        (await db.Categories.CountAsync()).Should().Be(0);
        (await db.Departments.CountAsync()).Should().Be(0);
    }

    [Then("the migration output identifies the destination and proposed additions")]
    public void PreviewOutput()
    {
        var output = fixture.Output.ToString();
        output.Should().Contain($"owner={fixture.Owner}").And.Contain("DRY RUN")
            .And.Contain("new=1").And.Contain("mapped=1").And.Contain("not-stocked=1")
            .And.Contain("unmapped=1").And.NotContain("Password");
    }

    [When("I write the synthetic migration and repeat it")]
    public async Task RepeatAsync()
    {
        fixture.FirstResult = await fixture.RunAsync(true);
        fixture.Before = await fixture.ReadAsync();
        fixture.Result = await fixture.RunAsync(true);
    }

    [Then("exactly one ingredient retains its source GUID and values")]
    public async Task ValuesAsync()
    {
        var row = await fixture.ReadAsync();
        row.Id.Should().Be(fixture.IngredientId);
        row.OwnerUserId.Should().Be(fixture.Owner);
        row.Name.Should().Be("Synthetic syrup");
        row.Category!.Name.Should().Be("Baking");
        row.Nutrition!.EnergyKcal.Should().Be(311);
        row.Nutrition.ProteinG.Should().Be(1.25m);
        row.Nutrition.SodiumMg.Should().Be(25);
        row.NutritionUpdatedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1769393852)
            .AddTicks(4740000).UtcDateTime);
        row.NutritionSource.Should().Be("synthetic");
        row.ProductLinks.Should().ContainSingle().Which.Should().Match<RecipeApp.Api.Infrastructure.Persistence.ProductLink>(
            link => link.Position == 0 && link.VariantLabel == "Golden" && link.Note == "large"
                && link.SupportsBothPricingMethods && link.IsPreferred);
        row.StoreMappings.Should().HaveCount(2);
        fixture.FirstResult!.Inserted.Should().Be(1);
    }

    [Then("the repeated migration is unchanged with stable child IDs")]
    public async Task UnchangedAsync()
    {
        fixture.Result!.Should().Be(new MigrationResult(0, 0, 1, 0, 0, 0, 0, 0));
        var row = await fixture.ReadAsync();
        row.Version.Should().Be(fixture.Before!.Version);
        row.ProductLinks.Single().Id.Should().Be(fixture.Before.ProductLinks.Single().Id);
    }

    [Given("the synthetic backup has already been imported and archived")]
    public async Task ImportAndArchiveAsync()
    {
        await fixture.RunAsync(true);
        await using var db = fixture.Database();
        var row = await db.Ingredients.SingleAsync();
        row.ArchivedAtUtc = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        fixture.Before = await fixture.ReadAsync();
    }

    [When("I review a replacement ingredient and respond (.*)")]
    public async Task ReviewAsync(string response)
    {
        fixture.ReplaceSource();
        fixture.Result = await fixture.RunAsync(true, response == "EOF" ? null : response);
    }

    [Then("the ingredient update is (.*) with audit and archive state preserved")]
    public async Task UpdatedAsync(string outcome)
    {
        var row = await fixture.ReadAsync();
        row.CreatedAtUtc.Should().Be(fixture.Before!.CreatedAtUtc);
        row.ArchivedAtUtc.Should().Be(fixture.Before.ArchivedAtUtc);
        row.OwnerUserId.Should().Be(fixture.Owner);
        var applied = outcome == "applied";
        fixture.Result!.Updated.Should().Be(applied ? 1 : 0);
        fixture.Result.Declined.Should().Be(applied ? 0 : 1);
        row.Name.Should().Be(applied ? "Replacement syrup" : "Synthetic syrup");
        row.ProductLinks.Should().HaveCount(applied ? 0 : 1);
        row.NutritionSource.Should().Be(applied ? null : "synthetic");
        if (applied)
        {
            row.StoreMappings.Should().ContainSingle().Which.IsNotStocked.Should().BeTrue();
            row.Nutrition.Should().BeNull();
        }
        else
        {
            row.Version.Should().Be(fixture.Before.Version);
            await using var db = fixture.Database();
            (await db.Categories.AnyAsync(category => category.Name == "Only applied")).Should().BeFalse();
            (await fixture.RunAsync(false)).Unchanged.Should().Be(0);
        }
        fixture.Output.ToString().Should().Contain("Links[woolworths:0]")
            .And.Contain("Nutrition.Source").And.Contain("Mappings[pak-n-save-tamatea]");
    }

    [When("I interrupt ingredient approval")]
    public async Task CancelAsync()
    {
        fixture.ReplaceSource();
        using var cancellation = new CancellationTokenSource();
        await fixture.CaptureFailureAsync(async () => await fixture.RunAsync(true, onApproval: () =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        }, token: cancellation.Token));
    }

    [Then("the reviewed plan is rejected without applying migration changes")]
    public async Task RejectedAsync()
    {
        fixture.Failure.Should().NotBeNull();
        var row = await fixture.ReadAsync();
        row.Name.Should().Be("Synthetic syrup");
        row.Category!.Name.Should().Be("Baking");
        row.ArchivedAtUtc.Should().Be(fixture.Before!.ArchivedAtUtc);
        await using var db = fixture.Database();
        (await db.Categories.AnyAsync(category => category.Name == "Only applied")).Should().BeFalse();
    }

    [When("a database write fails after some migration changes were saved")]
    public async Task RollbackAsync()
    {
        await fixture.CaptureFailureAsync(async () =>
            await fixture.RunAsync(true, interceptor: new FailSecondSave()));
        fixture.Failure.Should().NotBeNull();
    }

    private sealed class FailSecondSave : SaveChangesInterceptor
    {
        private int _calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++_calls == 2) throw new InvalidOperationException("Synthetic mid-write failure.");
            return ValueTask.FromResult(result);
        }
    }
}
