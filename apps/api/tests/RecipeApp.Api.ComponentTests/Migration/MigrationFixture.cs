using FirestoreMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Images;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.TestFixtures;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed record RecipeImportIdentity(Guid RecipeId, string? LegacyId, Guid OwnerUserId,
    IReadOnlyList<Guid> ChildIds, IReadOnlyList<Guid?> IngredientIds, string SourceDocumentJson,
    string DocumentSha256, string SourceFileSha256, Guid LastAppliedRunId);

public sealed partial class MigrationFixture(ApiAuthContext api) : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"recipe-migration-test-{Guid.NewGuid():N}");
    private readonly List<string> _recipeReportFiles = [];
    public Guid Owner { get; } = Guid.NewGuid();
    public Guid IngredientId { get; } = Guid.NewGuid();
    public string Password { get; } = Guid.NewGuid().ToString("N");
    public string BackupFile => Path.Combine(_directory, "ingredients.json");
    public string DecisionFile => Path.Combine(_directory, "decisions.json");
    public string RecipeFile => Path.Combine(_directory, "recipe-preview.json");
    public string RecipeReportFile => Path.Combine(_directory, "recipe-preview-report.json");
    public string ImageRoot => Path.Combine(_directory, "images");
    public string ImageManifestFile => Path.Combine(_directory, "image-manifest.json");
    public StringWriter Output { get; } = new();
    public MigrationResult? Result { get; set; }
    public MigrationResult? FirstResult { get; set; }
    public RecipeMigrationRunResult? FirstRecipeResult { get; set; }
    public RecipeMigrationRunResult? RecipeResult { get; set; }
    public string? RecipeAttemptReportPath { get; set; }
    public RecipeImportIdentity? FirstRecipeIdentity { get; set; }
    public string? StaleResource { get; set; }
    public Exception? Failure { get; set; }
    public Ingredient? Before { get; set; }
    public string? DecisionContents { get; set; }
    public string ConnectionString { get; private set; } = "";
    public IPrivateObjectMigrationStorage? ImageStorage { get; set; }
    public string? ImageJournalPath { get; set; }
    public byte[]? ImageBytes { get; set; }
    public IInterceptor? RecipeInterceptor { get; set; }
    public IRecipeReportPublisher? ReportPublisher { get; set; }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(BackupFile, OriginalJson());
        File.WriteAllText(DecisionFile, "{}");
        File.WriteAllText(Path.Combine(_directory, "meta.json"), """
            [{"id":"categories","list":["Baking"]},{"id":"departments","list":["Aisle 7"]},
             {"id":"supermarkets","supermarkets":[{"id":"woolworths-carlyle","chain":"Woolworths"}]}]
            """);
        // These unrelated files must never be opened, even when they are malformed.
        File.WriteAllText(Path.Combine(_directory, "users.json"), "NOT JSON");
        File.WriteAllText(Path.Combine(_directory, "recipes.json"), "NOT JSON");
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ConnectionString = db.Database.GetConnectionString()!;
        db.Users.Add(new User
        {
            Id = Owner, Email = $"migration-{Owner:N}@example.test",
            PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password),
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public AppDbContext Database(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new(builder.Options);
    }

    public async Task<MigrationResult> RunAsync(bool write, string? answer = "yes",
        Func<Task>? onApproval = null, CancellationToken token = default, IInterceptor? interceptor = null)
    {
        await using var db = Database(interceptor);
        TextReader input = onApproval is null ? new StringReader(answer is null ? "" : answer + "\n")
            : new MigrationApprovalReader(onApproval, answer);
        using (input)
            return await new MigrationRunner(db, input, Output)
                .RunAsync(new(_directory, Owner, DecisionFile, write), token);
    }

    public async Task<RecipeMigrationRunResult> RunRecipePreviewAsync(string recipeJson,
        string? selectedLegacyOwnerId = null, bool includeImageManifest = false,
        bool corruptImage = false)
    {
        return await RunRecipeAsync(recipeJson, false, RecipeReportFile, selectedLegacyOwnerId,
            includeImageManifest, corruptImage);
    }

    public string NewRecipeReportPath()
    {
        var path = Path.Combine(_directory, $"recipe-import-report-{Guid.NewGuid():N}.json");
        _recipeReportFiles.Add(path);
        return path;
    }

    public async Task SeedRecipeIngredientAsync()
    {
        await using var db = Database();
        db.Ingredients.Add(new Ingredient
        {
            Id = IngredientId,
            OwnerUserId = Owner,
            Name = "Synthetic flour",
            NormalizedName = "synthetic flour",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<RecipeImportIdentity> ReadRecipeIdentityAsync()
    {
        await using var db = Database();
        var recipe = await db.Recipes.AsNoTracking()
            .Include(row => row.Tags)
            .Include(row => row.Steps).ThenInclude(step => step.Items)
            .Include(row => row.ImportProvenance)
            .SingleAsync();
        var childIds = recipe.Tags.OrderBy(tag => tag.Position).Select(tag => tag.Id)
            .Concat(recipe.Steps.OrderBy(step => step.Position).SelectMany(step =>
                new[] { step.Id }.Concat(step.Items.OrderBy(item => item.Position).Select(item => item.Id))))
            .ToArray();
        var provenance = recipe.ImportProvenance
            ?? throw new InvalidOperationException("The imported Recipe has no provenance.");
        var ingredientIds = recipe.Steps.OrderBy(step => step.Position)
            .SelectMany(step => step.Items.OrderBy(item => item.Position))
            .Select(item => item.IngredientId)
            .ToArray();
        return new(recipe.Id, recipe.LegacyId, recipe.OwnerUserId, childIds, ingredientIds,
            provenance.SourceDocumentJson, provenance.DocumentSha256,
            provenance.SourceFileSha256, provenance.LastAppliedRunId);
    }

    public async Task<Ingredient> ReadAsync()
    {
        await using var db = Database();
        return await db.Ingredients.AsNoTracking().Include(row => row.Category)
            .Include(row => row.StoreMappings).ThenInclude(row => row.Store)
            .Include(row => row.StoreMappings).ThenInclude(row => row.Department)
            .Include(row => row.ProductLinks).ThenInclude(row => row.Chain)
            .SingleAsync(row => row.Id == IngredientId);
    }

    public string OriginalJson() => $$$$"""
        [{"id":"{{{{IngredientId}}}}","name":"Synthetic syrup","category":"Baking",
          "departments":{"woolworths-carlyle":"Aisle 7","pak-n-save-tamatea":"N/A"},
          "preferredVariant":"Golden","productUrls":{"woolworths":[
            {"url":"https://example.test/syrup","variantLabel":"Golden","note":"large","supportsBothPricingMethods":true}]},
          "nutrition":{"per100g":{"calories":311,"protein":1.25,"sodium":25},
            "source":"synthetic","lastUpdated":{"_seconds":1769393852,"_nanoseconds":474000000}}}]
        """;

    public void ReplaceSource() => File.WriteAllText(BackupFile, $$$$"""
        [{"id":"{{{{IngredientId}}}}","name":"Replacement syrup","category":"Only applied",
          "departments":{"woolworths-carlyle":"n/a"}}]
        """);

    public async Task CaptureFailureAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException
            or DbUpdateException or Npgsql.PostgresException)
        {
            Failure = exception;
        }
    }

    public void Dispose()
    {
        foreach (var name in new[] { "ingredients.json", "meta.json", "decisions.json", "users.json", "recipes.json",
                     "recipe-preview.json", "recipe-preview-report.json" })
        {
            File.Delete(Path.Combine(_directory, name));
            File.Delete(Path.Combine(_directory, name + ".lock"));
        }
        foreach (var reportFile in _recipeReportFiles)
        {
            File.Delete(reportFile);
            File.Delete(reportFile + ".lock");
        }
        File.Delete(ImageManifestFile);
        if (Directory.Exists(ImageRoot)) Directory.Delete(ImageRoot, recursive: true);
        Directory.Delete(_directory);
        Output.Dispose();
    }

}
