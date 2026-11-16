using System.Text.Json;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryMutationPersistence
{
    public static void TouchList(AppDbContext database, GroceryList list, DateTime nowUtc)
    {
        list.UpdatedAtUtc = nowUtc;
        database.Entry(list).Property(item => item.UpdatedAtUtc).IsModified = true;
        database.Entry(list).Property(item => item.Version).OriginalValue = list.Version;
    }

    public static GroceryCommandReceipt CreateReceipt(
        GroceryList list,
        Guid ownerId,
        Guid commandId,
        string commandType,
        string fingerprint,
        IReadOnlyList<Guid> resultIds,
        DateTime nowUtc)
    {
        var response = new GroceryCommandReceiptResponse(commandId, list.Version, resultIds);
        return new GroceryCommandReceipt
        {
            GroceryListId = list.Id,
            OwnerUserId = ownerId,
            CommandId = commandId,
            CommandType = commandType,
            RequestFingerprint = fingerprint,
            ResultJson = JsonSerializer.Serialize(response),
            ResultListVersion = list.Version,
            CommittedAtUtc = nowUtc
        };
    }
}
