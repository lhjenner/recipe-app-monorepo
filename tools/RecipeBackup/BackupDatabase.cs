using Npgsql;

namespace RecipeBackup;

internal static class BackupDatabase
{
    public static async Task<IReadOnlyList<BackupImage>> ImagesAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT i."RecipeId", r."OwnerUserId", i."ObjectKey", i."ContentType",
                   i."ByteSize", i."Sha256", i."Width", i."Height"
            FROM "RecipeImages" i JOIN "Recipes" r ON r."Id" = i."RecipeId"
            ORDER BY i."RecipeId"
            """, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(token);
        var result = new List<BackupImage>();
        while (await reader.ReadAsync(token))
            result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt64(4), reader.GetString(5).ToLowerInvariant(), reader.GetInt32(6), reader.GetInt32(7)));
        return result;
    }

    public static async Task RequireEmptyAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
              SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
              WHERE n.nspname <> 'information_schema' AND n.nspname !~ '^pg_'
                AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')
            )
            """, connection);
        if ((bool)(await command.ExecuteScalarAsync(token))!)
            throw new InvalidOperationException("Restore requires a new empty database; existing relations were found.");
    }
}
