namespace FirestoreMigration;

internal sealed class RecipeReportSession : IDisposable
{
    private readonly FileStream _lock;
    private readonly RecipeMigrationReport _initial;
    private readonly RecipeMigrationSource _source;
    private readonly string _path;
    private readonly IRecipeReportPublisher _publisher;
    private bool _published;
    public RecipeRunProgress Progress { get; }
    public IReadOnlyList<RecipeSourceRecord> Selected { get; }
    public IReadOnlySet<string> AmbiguousLegacyIds { get; }
    public RecipePreviewPlan Plan { get; set; } = new([], []);
    public RecipeSourceValidationResult Validation { get; set; } = new([], []);
    public IReadOnlyList<RecipeTargetRecord> Targets { get; set; } = [];
    public List<RecipeImageValidationResult> Images { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public List<string> Warnings { get; } = [];

    public RecipeReportSession(MigrationOptions options, RecipeMigrationSource source,
        DateTime started, string sourceHash, string decisionHash, string? manifestHash,
        string databaseScope, IRecipeReportPublisher? publisher = null)
    {
        _path = options.ReportFile!;
        _source = source;
        Progress = new(Guid.CreateVersion7(), options.Write);
        _publisher = publisher ?? new FileRecipeReportPublisher();
        var selection = RecipeSourceValidator.Select(source, options.SelectedLegacyOwnerIds);
        Selected = selection.Recipes;
        AmbiguousLegacyIds = selection.AmbiguousLegacyIds;
        Diagnostics.AddRange(selection.BlockingDiagnostics);
        _lock = new FileStream(_path + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(_path))
        {
            _lock.Dispose();
            throw new IOException("Report destination already exists; choose a new path.");
        }
        _initial = new(4, Progress.RunId, "previewing", started, started, options.Owner,
            sourceHash, decisionHash, source.Recipes.Count, Selected.Count,
            source.OwnerFieldCounts.Present, source.OwnerFieldCounts.Missing,
            source.MissingArchiveCount, source.MissingDateCount, source.BlankPlaceholders.Count,
            source.UnknownFieldPaths.Count, 0, 0, 0, 0, 0, manifestHash, 0,
            RecipeMigrationReportBuilder.BuildSummary(Selected, Validation),
            [], [], [], [], [], options.ImageJournalFile, [], WriteRequested: options.Write,
            DatabaseScopeSha256: databaseScope);
    }

    public RecipeMigrationReport Snapshot()
    {
        var outcomes = RecipeReportOutcomes.Recipes(_source, Selected, Plan, Progress, Diagnostics, Images, AmbiguousLegacyIds);
        var imageOutcomes = RecipeReportOutcomes.Images(_source, outcomes, Images, Targets, Progress);
        return _initial with
        {
            RunId = Progress.RunId, Status = Progress.Status, CompletedAtUtc = DateTime.UtcNow,
            ProposedInserts = Plan.ProposedInserts, ProposedUpdates = Plan.ProposedUpdates,
            Unchanged = Plan.Unchanged, CommittedInserts = Progress.Inserted,
            CommittedUpdates = Progress.Updated, ValidatedImages = Images.Count(image => image.Status == "validated"),
            CommittedUnchanged = Progress.Committed ? outcomes.Count(item => item.Outcome == "unchanged") : 0,
            Summary = RecipeMigrationReportBuilder.BuildSummary(Selected, Validation, outcomes),
            Images = Images.Select(image => image with
            {
                ImageReference = "sha256:" + RecipeSourceFingerprint.Create(image.ImageReference)
            }).ToArray(), Recipes = outcomes,
            SourceDispositions = RecipeMigrationReportBuilder.BuildSourceDispositions(_source,
                Selected.Select(recipe => recipe.LegacyId).ToHashSet(StringComparer.Ordinal)),
            Diagnostics = Diagnostics.Distinct(StringComparer.Ordinal).ToArray(),
            Warnings = _source.Warnings.Concat(Warnings).Distinct(StringComparer.Ordinal).ToArray(),
            JournalImages = Progress.JournalImages,
            ImageWork = RecipeReportOutcomes.ImageSummary(imageOutcomes, Progress),
            ImageOutcomes = imageOutcomes,
            OutcomeCounts = outcomes.GroupBy(item => item.Outcome, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            ApprovalDecisions = new Dictionary<string, bool>(Progress.Decisions, StringComparer.Ordinal)
        };
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(_path, Snapshot(), _published, cancellationToken);
        _published = true;
    }

    public async Task SaveFailureAsync(Exception failure, TextWriter output)
    {
        Progress.Fail(failure);
        Diagnostics.Add($"Run interrupted ({failure.GetType().Name}); " +
            (Progress.Committed ? "database commit succeeded."
                : Progress.CommitAttempted ? "commit outcome uncertain; reconcile database provenance and image journal."
                : "no accepted Recipes committed."));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await SaveAsync(timeout.Token);
            output.WriteLine($"Report: {_path}; run={Progress.RunId}, status={Progress.Status}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            PrintPersistenceFailure(output, exception);
        }
    }

    public void PrintPersistenceFailure(TextWriter output, Exception failure) =>
        output.WriteLine($"{(Progress.Committed ? "COMMITTED" : Progress.CommitAttempted ? "COMMIT UNCERTAIN" : "NOT COMMITTED")}: " +
            $"Recipe run {Progress.RunId}; report persistence failed ({failure.GetType().Name}). " +
            "Preserve the last checkpoint and reconcile database/provenance and image journal; do not assume rollback.");

    public void Dispose() => _lock.Dispose();
}
