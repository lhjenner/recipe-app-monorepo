namespace FirestoreMigration;

public interface IRecipeReportPublisher
{
    Task PublishAsync(string path, RecipeMigrationReport report, bool replace, CancellationToken cancellationToken);
}

public sealed class FileRecipeReportPublisher : IRecipeReportPublisher
{
    public Task PublishAsync(string path, RecipeMigrationReport report, bool replace, CancellationToken cancellationToken) =>
        RecipeMigrationReportWriter.WriteAsync(path, report, cancellationToken, replace);
}
