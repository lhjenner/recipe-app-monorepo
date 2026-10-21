using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Lookups;

public sealed class LookupService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser) : ILookupService
{
    public async Task<IReadOnlyList<LookupResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        return await database.Categories
            .AsNoTracking()
            .Where(category => category.OwnerUserId == currentUser.UserId)
            .OrderBy(category => category.Name)
            .Select(category => new LookupResponse(category.Id, category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<LookupResponse> CreateCategoryAsync(
        CreateLookupRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.UserId;
        var name = LookupName.Clean(request.Name);
        var normalizedName = LookupName.Normalize(name);

        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Categories.AsNoTracking()
            .SingleOrDefaultAsync(
                category => category.OwnerUserId == ownerUserId
                    && category.NormalizedName == normalizedName,
                cancellationToken);
        if (existing is not null)
        {
            return new LookupResponse(existing.Id, existing.Name);
        }

        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            Name = name,
            NormalizedName = normalizedName
        };
        database.Categories.Add(category);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return new LookupResponse(category.Id, category.Name);
        }
        catch (DbUpdateException exception) when (IsUniqueNameConflict(
            exception, "IX_Categories_OwnerUserId_NormalizedName"))
        {
            await using var retryDatabase =
                await databaseFactory.CreateDbContextAsync(cancellationToken);
            var racedCategory = await retryDatabase.Categories.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OwnerUserId == ownerUserId
                        && item.NormalizedName == normalizedName,
                    cancellationToken);
            if (racedCategory is null)
            {
                throw;
            }

            return new LookupResponse(racedCategory.Id, racedCategory.Name);
        }
    }

    public async Task<IReadOnlyList<LookupResponse>> GetDepartmentsAsync(
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        return await database.Departments
            .AsNoTracking()
            .Where(department => department.OwnerUserId == currentUser.UserId)
            .OrderBy(department => department.Name)
            .Select(department => new LookupResponse(department.Id, department.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<LookupResponse> CreateDepartmentAsync(
        CreateLookupRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.UserId;
        var name = LookupName.Clean(request.Name);
        var normalizedName = LookupName.Normalize(name);

        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Departments.AsNoTracking()
            .SingleOrDefaultAsync(
                department => department.OwnerUserId == ownerUserId
                    && department.NormalizedName == normalizedName,
                cancellationToken);
        if (existing is not null)
        {
            return new LookupResponse(existing.Id, existing.Name);
        }

        var department = new Department
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            Name = name,
            NormalizedName = normalizedName
        };
        database.Departments.Add(department);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return new LookupResponse(department.Id, department.Name);
        }
        catch (DbUpdateException exception) when (IsUniqueNameConflict(
            exception, "IX_Departments_OwnerUserId_NormalizedName"))
        {
            await using var retryDatabase =
                await databaseFactory.CreateDbContextAsync(cancellationToken);
            var racedDepartment = await retryDatabase.Departments.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OwnerUserId == ownerUserId
                        && item.NormalizedName == normalizedName,
                    cancellationToken);
            if (racedDepartment is null)
            {
                throw;
            }

            return new LookupResponse(racedDepartment.Id, racedDepartment.Name);
        }
    }

    public async Task<IReadOnlyList<SupermarketChainResponse>> GetSupermarketsAsync(
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var chains = await database.SupermarketChains
            .AsNoTracking()
            .Include(chain => chain.Stores)
            .OrderBy(chain => chain.Name)
            .ToListAsync(cancellationToken);

        return chains
            .Select(chain => new SupermarketChainResponse(
                chain.Code,
                chain.Name,
                chain.Stores
                    .OrderBy(store => store.DisplayOrder)
                    .Select(store => new StoreResponse(
                        store.Code,
                        store.Name,
                        store.DisplayOrder))
                    .ToArray()))
            .ToArray();
    }

    private static bool IsUniqueNameConflict(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException
        && postgresException.ConstraintName == constraintName;
}
