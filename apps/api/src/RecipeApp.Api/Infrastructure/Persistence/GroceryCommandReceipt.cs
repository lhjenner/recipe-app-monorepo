namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryCommandReceipt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroceryListId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid CommandId { get; set; }
    public required string CommandType { get; set; }
    public required string RequestFingerprint { get; set; }
    public string ResultJson { get; set; } = "{}";
    public long ResultListVersion { get; set; }
    public DateTime CommittedAtUtc { get; set; }
    public GroceryList? GroceryList { get; set; }
}
