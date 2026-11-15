using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RecipeApp.Api.Infrastructure.Persistence;

public static class GroceryListPersistence
{
    public static Task<GroceryList?> FindForOwnerAsync(
        AppDbContext db,
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        db.GroceryLists
            .AsNoTracking()
            .SingleOrDefaultAsync(list => list.OwnerUserId == ownerUserId, cancellationToken);

    public static async Task<GroceryList> GetOrCreateForMutationAsync(
        AppDbContext db,
        Guid ownerUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Grocery list creation must run inside the command transaction.");
        }

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The creation timestamp must be UTC.", nameof(nowUtc));
        }

        if (db.ChangeTracker.Entries().Any(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Create or load the grocery list before staging command changes.");
        }

        var existing = await db.GroceryLists
            .SingleOrDefaultAsync(list => list.OwnerUserId == ownerUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new GroceryList
        {
            OwnerUserId = ownerUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
        db.GroceryLists.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (IsOwnerUniqueViolation(exception))
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.GroceryLists
                .SingleAsync(list => list.OwnerUserId == ownerUserId, cancellationToken);
        }
    }

    private static bool IsOwnerUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_GroceryLists_OwnerUserId"
        };
}
