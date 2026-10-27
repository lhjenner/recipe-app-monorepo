using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Infrastructure.Storage;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var bucket = configuration["ObjectStorage:BucketName"];
if (string.IsNullOrWhiteSpace(bucket))
    throw new InvalidOperationException("Set ObjectStorage:BucketName to the private development bucket.");
var apiUrl = configuration["SmokeCheck:ApiUrl"] ?? "https://localhost:7209";
var email = $"neon-smoke-{Guid.NewGuid():N}@example.test";
var password = $"Synthetic-{Guid.NewGuid():N}!";
var objectKey = $"smoke-tests/{Guid.NewGuid():N}.png";
var bytes = RecipeApp.TestFixtures.SyntheticImage.Png;
var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
using var s3 = ObjectStorageRegistration.CreateClient(configuration);
var storage = new S3PrivateObjectStorage(s3, Options.Create(new ObjectStorageOptions { BucketName = bucket }));
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(DatabaseConnection.Get(configuration)).Options);
using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
using var client = new HttpClient(handler) { BaseAddress = new Uri(apiUrl) };
using var anonymous = new HttpClient { BaseAddress = client.BaseAddress };
Guid? ownerId = null;
var registrationAttempted = false;
var objectAttempted = false;
var phase = "checking EF migrations";

try
{
    Require(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Apply EF migrations before the live smoke check.");
    Console.WriteLine("PASS: Neon Postgres is reachable and EF migrations are current.");

    phase = "registering and signing in through the API";
    registrationAttempted = true;
    using var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
    Require(register.StatusCode == HttpStatusCode.Created, "API registration did not return 201.");
    var user = await register.Content.ReadFromJsonAsync<JsonElement>();
    ownerId = user.GetProperty("id").GetGuid();
    using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    Require(login.StatusCode == HttpStatusCode.OK, "API login did not return 200.");
    using var me = await client.GetAsync("/api/auth/me");
    Require(me.StatusCode == HttpStatusCode.OK, "Authenticated API session did not persist.");
    Console.WriteLine("PASS: API registration, login and persistent Postgres session.");

    phase = "uploading and downloading the synthetic private object";
    objectAttempted = true;
    await using (var input = new MemoryStream(bytes))
        await storage.WriteAsync(objectKey, input);
    await using (var output = await storage.OpenReadAsync(objectKey))
    {
        Require(output is not null, "Uploaded synthetic object could not be read.");
        using var buffer = new MemoryStream();
        await output!.CopyToAsync(buffer);
        Require(buffer.ToArray().SequenceEqual(bytes), "Private storage round trip changed the bytes.");
    }
    using var publicRead = await anonymous.GetAsync(
        $"{configuration["AWS_ENDPOINT_URL_S3"]!.TrimEnd('/')}/{bucket}/{objectKey}");
    Require(publicRead.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
        "Anonymous bucket read was not denied.");
    Console.WriteLine("PASS: .NET S3 write/read matches original bytes; anonymous bucket read denied.");

    phase = "associating and delivering the image through the authenticated API";
    var recipe = new Recipe
    {
        OwnerUserId = ownerId.Value,
        Name = "Synthetic Neon Smoke Check",
        NormalizedName = "synthetic neon smoke check",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        Image = new RecipeImage
        {
            ObjectKey = objectKey, ContentType = "image/png", ByteSize = bytes.Length,
            Sha256 = checksum, Width = 1, Height = 1
        }
    };
    db.Recipes.Add(recipe);
    await db.SaveChangesAsync();
    var imagePath = $"/api/recipes/{recipe.Id}/image";
    using var image = await client.GetAsync(imagePath);
    Require(image.StatusCode == HttpStatusCode.OK, "Image endpoint did not return 200.");
    Require((await image.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes), "Image endpoint checksum mismatch.");
    Require(image.Content.Headers.ContentType?.MediaType == "image/png", "Image Content-Type mismatch.");
    Require(image.Headers.CacheControl is { Private: true, NoCache: true }, "Private cache headers missing.");
    Require(image.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "nosniff header missing.");
    using var request = new HttpRequestMessage(HttpMethod.Get, imagePath);
    request.Headers.IfNoneMatch.Add(image.Headers.ETag!);
    using var conditional = await client.SendAsync(request);
    Require(conditional.StatusCode == HttpStatusCode.NotModified, "Conditional image request did not return 304.");
    using var anonymousImage = await anonymous.GetAsync(imagePath);
    Require(anonymousImage.StatusCode == HttpStatusCode.Unauthorized, "Anonymous image request did not return 401.");
    Console.WriteLine("PASS: Postgres association -> private cloud bytes -> authenticated API; exact SHA-256, 200/304/401.");
}
catch (Exception exception) when (exception is HttpRequestException or AmazonServiceException
    or DbException or InvalidOperationException or IOException)
{
    Console.Error.WriteLine($"FAIL: {phase} ({exception.GetType().Name}). No credentials are printed.");
    if (exception is InvalidOperationException) Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        try
        {
            if (registrationAttempted)
            {
                using var logout = await client.PostAsync("/api/auth/logout", null);
                Require(logout.StatusCode == HttpStatusCode.NoContent, "Synthetic session logout failed.");
            }
        }
        finally
        {
            if (registrationAttempted)
            {
                // The unique synthetic email bounds cleanup even if registration succeeded before a network failure.
                await db.Users.Where(row => row.Email == email).ExecuteDeleteAsync();
                Require(!await db.Users.AnyAsync(row => row.Email == email), "Synthetic account cleanup failed.");
                Console.WriteLine("CLEANUP: synthetic account, recipe and association removed.");
            }
        }
    }
    finally
    {
        if (objectAttempted)
        {
            await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = objectKey });
            await using var missing = await storage.OpenReadAsync(objectKey);
            Require(missing is null, "Synthetic object cleanup failed.");
            Console.WriteLine("CLEANUP: synthetic object deleted and no longer readable.");
        }
    }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
