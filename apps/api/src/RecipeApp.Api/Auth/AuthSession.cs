namespace RecipeApp.Api.Auth;

public sealed class AuthSession
{
    public required string Id { get; set; }
    public required byte[] TicketData { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
