namespace RecipeApp.Api.ComponentTests.Migration;

internal sealed class RecipeApprovalReader(string ownerResponse, Func<Task> onApproval, string? approval)
    : TextReader
{
    private bool _ownerResponseRead;

    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_ownerResponseRead)
        {
            _ownerResponseRead = true;
            return ownerResponse;
        }

        await onApproval();
        cancellationToken.ThrowIfCancellationRequested();
        return approval;
    }
}
