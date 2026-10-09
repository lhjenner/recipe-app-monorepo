using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Auth;

[Binding]
public sealed class AuthHooks(ApiAuthContext context)
{
    [BeforeScenario]
    public Task StartDatabaseAsync() => context.StartAsync();

    [AfterScenario]
    public async Task DisposeDatabaseAsync()
    {
        await context.DisposeAsync();
    }
}
