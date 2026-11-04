using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Infrastructure.Storage;

namespace FirestoreMigration;

internal static class RecipeImageCli
{
    public static S3PrivateObjectStorage Storage(IConfiguration configuration, Amazon.S3.IAmazonS3 client)
    {
        var bucket = configuration["ObjectStorage:BucketName"];
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("Configure ObjectStorage:BucketName explicitly for private image migration.");
        return new(client, Options.Create(new ObjectStorageOptions { BucketName = bucket }));
    }

    public static async Task<int> RecoverAsync(string[] args, AppDbContext db,
        IConfiguration configuration, TextWriter output, CancellationToken cancellationToken)
    {
        if (args.Length is not (2 or 3) || args.Length == 3 && args[2] != "--cleanup-staged-images")
            throw new FormatException("Recovery: --recover-image-journal <file> [--cleanup-staged-images].");
        using var journal = RecipeImageJournal.OpenExisting(args[1]);
        using var client = ObjectStorageRegistration.CreateClient(configuration);
        var result = await RecipeImageRecovery.RecoverAsync(db, Storage(configuration, client),
            journal, args.Length == 3, cancellationToken);
        output.WriteLine($"Image recovery: run={result.RunId}, committed={result.Committed}, " +
            $"referenced={result.Referenced}, unreferenced={result.Unreferenced}, missing={result.Missing}, deleted={result.Deleted}.");
        output.WriteLine("Superseded images outside this staging journal were retained; cleanup is not bucket-wide.");
        return 0;
    }
}
