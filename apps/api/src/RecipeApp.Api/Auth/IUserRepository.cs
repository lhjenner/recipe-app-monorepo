namespace RecipeApp.Api.Auth;

// Contract: "anything that can answer questions about / persist users."
// Async with CancellationToken passthrough per project guidelines.
// The real implementation will use EF Core; tests use a substitute.
public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}
