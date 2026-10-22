using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Ingredients;
using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Migration;

[Binding]
public sealed class MigrationIntegritySteps(MigrationFixture fixture, ApiAuthContext api)
{
    private bool _competingWriteBlocked;
    private Guid[]? _linkIds;

    [Then("the imported ingredient is readable through the authenticated API")]
    public async Task ApiValuesAsync()
    {
        using var login = await api.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest($"migration-{fixture.Owner:N}@example.test", fixture.Password));
        login.EnsureSuccessStatusCode();
        using var response = await api.Client.GetAsync($"/api/ingredients/{fixture.IngredientId}");
        response.EnsureSuccessStatusCode();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var value = await response.Content.ReadFromJsonAsync<IngredientDetailResponse>(jsonOptions);
        value!.Id.Should().Be(fixture.IngredientId);
        value.Name.Should().Be("Synthetic syrup");
        value.ProductLinks.Single().Should().Match<IngredientProductLinkResponse>(link =>
            link.Position == 0 && link.VariantLabel == "Golden" && link.Note == "large"
            && link.SupportsBothPricingMethods && link.IsPreferred);
        value.Nutrition!.EnergyKcal.Should().Be(311);
        value.NutritionUpdatedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1769393852).AddTicks(4740000));
    }

    [When("another writer attempts an insertion while the migration commits")]
    public async Task ConcurrentInsertAsync()
    {
        var interceptor = new OnFirstSave(async () =>
        {
            await using var other = fixture.Database();
            await using var transaction = await other.Database.BeginTransactionAsync();
            await other.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '200ms'");
            other.Categories.Add(new() { Id = Guid.NewGuid(), OwnerUserId = fixture.Owner,
                Name = "Competing writer", NormalizedName = "competing writer" });
            try { await other.SaveChangesAsync(); }
            catch (Exception exception) when ((exception is DbUpdateException or InvalidOperationException) &&
                exception.GetBaseException() is PostgresException { SqlState: "55P03" })
            {
                _competingWriteBlocked = true;
            }
        });
        fixture.Result = await fixture.RunAsync(true, interceptor: interceptor);
    }

    [Then("the migration commits and the competing write is blocked")]
    public async Task ConcurrentInsertBlockedAsync()
    {
        _competingWriteBlocked.Should().BeTrue();
        fixture.Result!.Inserted.Should().Be(1);
        await using var db = fixture.Database();
        (await db.Categories.AnyAsync(row => row.Name == "Competing writer")).Should().BeFalse();
    }

    [When("I switch preference between two imported product links")]
    public async Task SwitchPreferredAsync()
    {
        var json = fixture.OriginalJson().Replace(
            "\"supportsBothPricingMethods\":true}]",
            "\"supportsBothPricingMethods\":true},{\"url\":\"https://example.test/other\",\"variantLabel\":\"Other\"}]");
        File.WriteAllText(fixture.BackupFile, json);
        await fixture.RunAsync(true);
        _linkIds = (await fixture.ReadAsync()).ProductLinks.OrderBy(row => row.Position).Select(row => row.Id).ToArray();
        File.WriteAllText(fixture.BackupFile, json.Replace("\"preferredVariant\":\"Golden\"", "\"preferredVariant\":\"Other\""));
        fixture.Result = await fixture.RunAsync(true);
    }

    [Then("the new preference persists with the same link IDs")]
    public async Task PreferredAsync()
    {
        var row = await fixture.ReadAsync();
        row.ProductLinks.OrderBy(link => link.Position).Select(link => link.Id).Should().Equal(_linkIds!);
        row.ProductLinks.Single(link => link.IsPreferred).VariantLabel.Should().Be("Other");
        fixture.Result!.Updated.Should().Be(1);
        (await fixture.RunAsync(true)).Unchanged.Should().Be(1);
    }

    [When("I import an empty newer ingredient backup")]
    public async Task AbsentAsync()
    {
        File.WriteAllText(fixture.BackupFile, "[]");
        fixture.Result = await fixture.RunAsync(true);
    }

    [Then("the absent ingredient keeps all values and its version")]
    public async Task AbsentPreservedAsync()
    {
        var row = await fixture.ReadAsync();
        TargetSnapshot.Value(row).Should().BeEquivalentTo(TargetSnapshot.Value(fixture.Before!));
        row.Version.Should().Be(fixture.Before!.Version);
        row.CreatedAtUtc.Should().Be(fixture.Before.CreatedAtUtc);
        row.ArchivedAtUtc.Should().Be(fixture.Before.ArchivedAtUtc);
    }

    [When("a required (.*) is unavailable for migration")]
    public async Task MissingAsync(string requirement)
    {
        switch (requirement)
        {
            case "ingredients": File.Delete(fixture.BackupFile); break;
            case "meta": File.Delete(Path.Combine(Path.GetDirectoryName(fixture.BackupFile)!, "meta.json")); break;
            case "decisions": File.Delete(fixture.DecisionFile); break;
            case "owner":
                await using (var db = fixture.Database())
                {
                    db.Users.Remove(await db.Users.SingleAsync());
                    await db.SaveChangesAsync();
                }
                break;
            case "schema":
                await using (var db = fixture.Database())
                {
                    var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
                    await migrator.MigrateAsync("20261021213500_AddIngredientLookups");
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(requirement));
        }
        try { await fixture.RunAsync(true); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            fixture.Failure = exception;
        }
    }

    [Then("an explicit migration failure leaves no ingredient writes")]
    public void MissingFails()
    {
        fixture.Failure.Should().NotBeNull();
        fixture.Failure!.Message.Should().NotBeNullOrWhiteSpace();
        fixture.Output.ToString().Should().NotContain("Finished: inserted=1");
    }

    private sealed class OnFirstSave(Func<Task> callback) : SaveChangesInterceptor
    {
        private bool _called;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_called)
            {
                _called = true;
                await callback();
            }
            return result;
        }
    }
}
