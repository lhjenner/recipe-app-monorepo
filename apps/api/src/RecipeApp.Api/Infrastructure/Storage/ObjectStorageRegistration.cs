using Amazon.Runtime;
using Amazon.S3;
using RecipeApp.Api.Images;

namespace RecipeApp.Api.Infrastructure.Storage;

public static class ObjectStorageRegistration
{
    public static IServiceCollection AddPrivateObjectStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName),
                "Configure ObjectStorage:BucketName before accessing images.");
        services.AddSingleton<IAmazonS3>(_ => CreateClient(configuration));
        services.AddSingleton<IPrivateObjectStorage, S3PrivateObjectStorage>();
        return services;
    }

    public static IAmazonS3 CreateClient(IConfiguration configuration)
    {
        var endpoint = configuration["AWS_ENDPOINT_URL_S3"];
        var region = configuration["AWS_REGION"];
        var accessKey = configuration["AWS_ACCESS_KEY_ID"];
        var secretKey = configuration["AWS_SECRET_ACCESS_KEY"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Private storage requires an HTTPS AWS_ENDPOINT_URL_S3, AWS_REGION, AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY.");
        }

        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = region,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
    }
}
