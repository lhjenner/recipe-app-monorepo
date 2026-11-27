using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryReadService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IGroceryReadService
{
    public async Task<GroceryListResponse> GetListAsync(
        GroceryListQuery query,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ownerUserId = currentUser.UserId;
        var list = await database.GroceryLists
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerUserId == ownerUserId, cancellationToken);
        if (list is null)
        {
            return new GroceryListResponse(null, 0, 0, []);
        }

        var completedItemCount = await database.GroceryItems
            .AsNoTracking()
            .CountAsync(item => item.OwnerUserId == ownerUserId
                && item.GroceryListId == list.Id
                && item.State == GroceryItemState.Completed,
                cancellationToken);
        var completedRange = GetCompletedRange(query);
        var itemsQuery = database.GroceryItems
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId
                && item.GroceryListId == list.Id
                && (item.State == GroceryItemState.Outstanding
                    || (query.IncludeCompleted
                        && item.State == GroceryItemState.Completed
                        && item.PurchaseEvents.Any(purchaseEvent =>
                            purchaseEvent.Kind == GroceryPurchaseEventKind.Purchase
                            && (completedRange == null
                                || (purchaseEvent.OccurredAtUtc >= completedRange.Value.StartUtc
                                    && purchaseEvent.OccurredAtUtc < completedRange.Value.EndExclusiveUtc))))));

        var items = await itemsQuery
            .Include(item => item.Ingredient)
                .ThenInclude(ingredient => ingredient!.StoreMappings)
                    .ThenInclude(mapping => mapping.Department)
            .Include(item => item.Department)
            .Include(item => item.Contributions)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var responses = items
            .OrderBy(item => item.NormalizedName)
            .ThenBy(item => item.Id)
            .Select(GroceryReadResponseMapper.MapItem)
            .ToArray();

        return new GroceryListResponse(list.Id, list.Version, completedItemCount, responses);
    }

    public async Task<GroceryHistoryResponse?> GetHistoryAsync(
        GroceryHistoryQuery query,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ownerUserId = currentUser.UserId;
        var listId = await database.GroceryLists
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (listId is null)
        {
            return query.Page > 1
                ? null
                : new GroceryHistoryResponse([], 0, query.Page, query.PageSize);
        }

        var range = GetHistoryRange(query);
        var historyQuery = database.GroceryPurchaseEvents
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId && item.GroceryListId == listId.Value);
        if (range is not null)
        {
            historyQuery = historyQuery.Where(item =>
                item.OccurredAtUtc >= range.Value.StartUtc
                && item.OccurredAtUtc < range.Value.EndExclusiveUtc);
        }

        var totalCount = await historyQuery.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)query.PageSize));
        if (query.Page > pageCount)
        {
            return null;
        }

        var events = await historyQuery
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var purchaseIds = events
            .Where(item => item.Kind == GroceryPurchaseEventKind.Purchase)
            .Select(item => item.Id)
            .ToArray();
        var reversedPurchaseIds = purchaseIds.Length == 0
            ? new HashSet<Guid>()
            : await database.GroceryPurchaseEvents
                .AsNoTracking()
                .Where(item => item.OwnerUserId == ownerUserId
                    && item.GroceryListId == listId.Value
                    && item.Kind == GroceryPurchaseEventKind.Reversal
                    && item.ReversesEventId != null
                    && purchaseIds.Contains(item.ReversesEventId.Value))
                .Select(item => item.ReversesEventId!.Value)
                .ToHashSetAsync(cancellationToken);

        var storeIds = events
            .Where(item => item.StoreNameSnapshot is null && item.StoreIdSnapshot is not null)
            .Select(item => item.StoreIdSnapshot!.Value)
            .Distinct()
            .ToArray();
        var storeNames = storeIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await database.Stores
                .AsNoTracking()
                .Where(store => storeIds.Contains(store.Id))
                .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        return new GroceryHistoryResponse(
            events.Select(item => new GroceryPurchaseEventResponse(
                item.Id,
                item.GroceryItemId,
                item.Kind,
                item.Quantity,
                item.PurchaseUnit,
                AsUtcOffset(item.OccurredAtUtc),
                item.ItemNameSnapshot,
                item.NoteSnapshot,
                item.StoreIdSnapshot,
                item.StoreNameSnapshot
                    ?? (item.StoreIdSnapshot is not null && storeNames.TryGetValue(item.StoreIdSnapshot.Value, out var storeName)
                        ? storeName
                        : null),
                item.Origin,
                item.ReversesEventId,
                reversedPurchaseIds.Contains(item.Id))).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private AucklandUtcRange? GetCompletedRange(GroceryListQuery query)
    {
        if (!query.IncludeCompleted)
        {
            return null;
        }

        var today = AucklandDateRange.Today(timeProvider);
        return AucklandDateRange.ForDates(query.From ?? today, query.To ?? today);
    }

    private AucklandUtcRange? GetHistoryRange(GroceryHistoryQuery query)
    {
        if (query.AllDates)
        {
            return null;
        }

        var today = AucklandDateRange.Today(timeProvider);
        return AucklandDateRange.ForDates(query.From ?? today, query.To ?? today);
    }

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
