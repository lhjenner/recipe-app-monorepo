namespace FirestoreMigration;

public sealed class RecipeRunProgress(Guid runId, bool writeRequested)
{
    public Guid RunId { get; set; } = runId;
    public string Status { get; set; } = "previewing";
    public bool CommitAttempted { get; private set; }
    public bool Committed { get; private set; }
    public int Inserted { get; private set; }
    public int Updated { get; private set; }
    public IReadOnlyList<RecipePreviewItem> Results { get; private set; } = [];
    public Dictionary<string, bool> Decisions { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<JournalImage> JournalImages { get; set; } = [];
    public HashSet<string> UploadedKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ReusedKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> VerifiedKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> PlannedKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UploadAttemptedKeys { get; } = new(StringComparer.Ordinal);

    public void PreparingCommit()
    {
        CommitAttempted = true;
        Status = "commit-uncertain";
    }

    public void ConfirmCommit(int inserted, int updated, IReadOnlyList<RecipePreviewItem> results)
    {
        Committed = true;
        Status = "committed";
        Inserted = inserted;
        Updated = updated;
        Results = results;
    }

    public void Fail(Exception exception)
    {
        if (Committed) Status = "committed";
        else if (CommitAttempted) Status = "commit-uncertain";
        else Status = exception is OperationCanceledException ? "cancelled"
            : writeRequested ? "rolled-back" : "operational-failure";
    }
}
