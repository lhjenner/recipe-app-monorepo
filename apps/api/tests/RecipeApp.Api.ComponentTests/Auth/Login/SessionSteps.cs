using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Auth.Login;

[Binding]
public sealed class SessionSteps(ApiAuthContext context)
{
    [When("I request my current profile with the session cookie {string}")]
    public async Task WhenIRequestMyCurrentProfileWithTheSessionCookie(string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", cookie);
        context.Response = await context.Client.SendAsync(request);
    }

    [When("{int} days pass")]
    public void WhenDaysPass(int days) => context.Time.Advance(TimeSpan.FromDays(days));

    [When("{int} days and {int} minute pass")]
    public void WhenDaysAndMinutesPass(int days, int minutes) =>
        context.Time.Advance(TimeSpan.FromDays(days) + TimeSpan.FromMinutes(minutes));
}
