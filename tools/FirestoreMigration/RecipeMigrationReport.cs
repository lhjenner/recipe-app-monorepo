namespace FirestoreMigration;

public sealed record RecipeMigrationRunResult(int Errors, string Status, string ReportPath);

public sealed record RecipeMigrationReport(
    int SchemaVersion,
    Guid RunId,
    string Status,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    Guid DestinationOwnerId,
    string SourceFileSha256,
    string DecisionFileSha256,
    int SourceRecipes,
    int SelectedSourceRecipes,
    int LegacyOwnerFieldsPresent,
    int LegacyOwnerFieldsMissing,
    int MissingArchiveFields,
    int MissingDateFields,
    int BlankPlaceholdersOmitted,
    int UnknownFieldOccurrences,
    int ProposedInserts,
    int ProposedUpdates,
    int Unchanged,
    int CommittedInserts,
    int CommittedUpdates,
    string? ImageManifestSha256,
    int ValidatedImages,
    RecipeMigrationRunSummary Summary,
    IReadOnlyList<RecipeImageValidationResult> Images,
    IReadOnlyList<RecipeMigrationSourceDisposition> SourceDispositions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<RecipePreviewItem> Recipes,
    string? ImageJournalPath = null,
    IReadOnlyList<JournalImage>? JournalImages = null,
    RecipeImageWorkSummary? ImageWork = null,
    IReadOnlyList<RecipeImageWork>? ImageOutcomes = null,
    IReadOnlyDictionary<string, int>? OutcomeCounts = null,
    IReadOnlyDictionary<string, bool>? ApprovalDecisions = null,
    string Mode = "recipes", bool WriteRequested = false,
    string? DatabaseScopeSha256 = null, int CommittedUnchanged = 0);

public sealed record RecipeImageWork(string LegacyId, string PlannedAction, string Outcome,
    string? ObjectKey, string? Sha256, string? ImageReferenceSha256 = null,
    string? PlannedSha256 = null, string? PreviousObjectKey = null);

public sealed record RecipeImageWorkSummary(int PlannedUploads, int PlannedRemovals,
    int Uploaded, int VerifiedStaged, int ReusedStaged, int UploadUncertain, int CommittedAssociations,
    int CommittedRemovals, int UnreferencedStaged, int CommitUncertain, int RetainedAssociations);
