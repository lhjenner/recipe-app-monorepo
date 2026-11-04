namespace RecipeApp.Api.ComponentTests.Migration;

internal sealed class MigrationApprovalReader(Func<Task> callback, string? answer) : TextReader
{
    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        await callback();
        return answer;
    }
}
