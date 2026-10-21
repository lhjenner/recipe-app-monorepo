using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class UserRepository(AppDbContext database) : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        database.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        database.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        database.Users.Add(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            throw new DuplicateEmailException();
        }
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        // Entities from the Get* methods are tracked; attaching is a no-op for them.
        database.Users.Update(user);
        return database.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Users_Email"
        };
}
