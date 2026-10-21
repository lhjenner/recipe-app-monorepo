namespace RecipeApp.Api.Infrastructure.Persistence;

public interface ICurrentUser
{
    Guid UserId { get; }
}
