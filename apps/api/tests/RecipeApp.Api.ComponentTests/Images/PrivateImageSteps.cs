using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Images;

[Binding]
public sealed class PrivateImageSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private string? _etag;

    [Given("a signed-in synthetic image owner with an active recipe image")]
    public async Task SeedAsync()
    {
        var email = $"image-{Guid.NewGuid():N}@example.test";
        var password = $"Synthetic-{Guid.NewGuid():N}!";
        await context.SeedExistingAccountAsync(email, password);
        using var login = await context.Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var owner = (await context.FindUserAsync(email))!;
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checksum = Convert.ToHexString(SHA256.HashData(SyntheticObjectStorage.ImageBytes));
        var recipe = new Recipe
        {
            OwnerUserId = owner.Id,
            Name = "Synthetic Image",
            NormalizedName = "synthetic image",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Image = new RecipeImage
            {
                ObjectKey = "synthetic/test.png",
                ContentType = "image/png",
                ByteSize = SyntheticObjectStorage.ImageBytes.Length,
                Sha256 = checksum,
                Width = 1,
                Height = 1
            }
        };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        _recipeId = recipe.Id;
        _etag = $"\"{checksum.ToLowerInvariant()}\"";
        context.Storage.Content = SyntheticObjectStorage.ImageBytes;
    }

    [Given("the recipe image is inaccessible because it is (.*)")]
    public async Task MakeInaccessibleAsync(string reason)
    {
        if (reason == "anonymous")
        {
            using var logout = await context.Client.PostAsync("/api/auth/logout", null);
            logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
            return;
        }
        if (reason == "another owner")
        {
            var email = $"other-image-{Guid.NewGuid():N}@example.test";
            var password = $"Synthetic-{Guid.NewGuid():N}!";
            await context.SeedExistingAccountAsync(email, password);
            using var login = await context.Client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            return;
        }
        if (reason == "missing")
        {
            _recipeId = Guid.NewGuid();
            return;
        }
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (reason == "archived")
            (await db.Recipes.SingleAsync(row => row.Id == _recipeId)).IsArchived = true;
        else if (reason == "absent")
            db.RecipeImages.Remove(await db.RecipeImages.SingleAsync(row => row.RecipeId == _recipeId));
        else
            throw new InvalidOperationException($"Unknown synthetic scenario: {reason}");
        await db.SaveChangesAsync();
    }

    [Given("synthetic image storage is (.*)")]
    public void FailStorage(string failure)
    {
        switch (failure)
        {
            case "unavailable": context.Storage.Unavailable = true; break;
            case "missing": context.Storage.Content = null; break;
            case "corrupt": context.Storage.Content = new byte[SyntheticObjectStorage.ImageBytes.Length]; break;
            default: throw new InvalidOperationException($"Unknown synthetic failure: {failure}");
        }
    }

    [When("the recipe image is requested")]
    public async Task RequestAsync() =>
        context.Response = await context.Client.GetAsync($"/api/recipes/{_recipeId}/image");

    [When("the recipe image is requested with its current ETag")]
    public async Task ConditionalAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/recipes/{_recipeId}/image");
        request.Headers.TryAddWithoutValidation("If-None-Match", _etag);
        context.Response = await context.Client.SendAsync(request);
    }

    [When("the same conditional image request is made anonymously")]
    public async Task AnonymousConditionalAsync()
    {
        using var logout = await context.Client.PostAsync("/api/auth/logout", null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConditionalAsync();
    }

    [Then("the exact synthetic bytes and private image headers are returned")]
    public async Task AssertImageAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        (await context.Response.Content.ReadAsByteArrayAsync()).Should().Equal(SyntheticObjectStorage.ImageBytes);
        context.Response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        context.Response.Headers.CacheControl!.Private.Should().BeTrue();
        context.Response.Headers.CacheControl.NoCache.Should().BeTrue();
        context.Response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        context.Response.Headers.ETag!.ToString().Should().Be(_etag);
    }

    [Then("the image response is {int}")]
    public void AssertStatus(int status) => ((int)context.Response!.StatusCode).Should().Be(status);

    [Then("the image response is {int} and storage has not been read")]
    public void AssertNoStorageRead(int status)
    {
        AssertStatus(status);
        context.Storage.Reads.Should().Be(0);
    }

    [Then("the image response is Problem Details")]
    public void AssertProblem() =>
        context.Response!.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

}
