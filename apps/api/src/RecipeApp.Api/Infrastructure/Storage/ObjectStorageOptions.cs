namespace RecipeApp.Api.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string BucketName { get; set; } = "";
}
