using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public static class RecipeTargetFingerprint
{
    public static async Task<string> ComputeAsync(AppDbContext db, Guid ownerId,
        IReadOnlyCollection<string> legacyIds, IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken)
    {
        var orderedLegacyIds = legacyIds.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        var orderedSourceIds = sourceIds.Distinct().Order().ToArray();
        var versions = new List<string>();
        if (orderedLegacyIds.Length > 0)
        {
            versions.AddRange(await db.Database.SqlQuery<string>($"""
                SELECT 'recipe:' || recipe."Id"::text || ':' || recipe.xmin::text AS "Value"
                FROM "Recipes" recipe
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                UNION ALL
                SELECT 'tag:' || tag."Id"::text || ':' || tag.xmin::text
                FROM "RecipeTags" tag
                JOIN "Recipes" recipe ON recipe."Id" = tag."RecipeId"
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                UNION ALL
                SELECT 'step:' || step."Id"::text || ':' || step.xmin::text
                FROM "RecipeSteps" step
                JOIN "Recipes" recipe ON recipe."Id" = step."RecipeId"
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                UNION ALL
                SELECT 'item:' || item."Id"::text || ':' || item.xmin::text
                FROM "RecipeStepItems" item
                JOIN "RecipeSteps" step ON step."Id" = item."RecipeStepId"
                JOIN "Recipes" recipe ON recipe."Id" = step."RecipeId"
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                UNION ALL
                SELECT 'image:' || image."RecipeId"::text || ':' || image.xmin::text
                FROM "RecipeImages" image
                JOIN "Recipes" recipe ON recipe."Id" = image."RecipeId"
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                UNION ALL
                SELECT 'provenance:' || provenance."RecipeId"::text || ':' || provenance.xmin::text
                FROM "RecipeImportProvenance" provenance
                JOIN "Recipes" recipe ON recipe."Id" = provenance."RecipeId"
                WHERE recipe."OwnerUserId" = {ownerId}
                    AND recipe."LegacyId" = ANY({orderedLegacyIds})
                """).ToListAsync(cancellationToken));
        }
        if (orderedSourceIds.Length > 0)
        {
            versions.AddRange(await db.Database.SqlQuery<string>($"""
                SELECT 'ingredient:' || ingredient."Id"::text || ':' || ingredient.xmin::text AS "Value"
                FROM "Ingredients" ingredient
                WHERE ingredient."Id" = ANY({orderedSourceIds})
                """).ToListAsync(cancellationToken));
        }

        var snapshot = string.Join('\n', versions.Order(StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }
}
