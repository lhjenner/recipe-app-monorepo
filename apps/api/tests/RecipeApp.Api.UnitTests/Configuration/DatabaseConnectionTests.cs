using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.UnitTests.Configuration;

public sealed class DatabaseConnectionTests
{
    [Fact]
    public void NeonUrl_PreservesEscapedCredentialsAndRequiresVerifiedTls()
    {
        var configuration = Configure("DATABASE_URL",
            "postgresql://test%40user:p%3Ass%2Bword@example.test:5433/recipe%20app?sslmode=require&channel_binding=require");

        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.Get(configuration));

        result.Host.Should().Be("example.test");
        result.Port.Should().Be(5433);
        result.Username.Should().Be("test@user");
        result.Password.Should().Be("p:ss+word");
        result.Database.Should().Be("recipe app");
        result.SslMode.Should().Be(SslMode.VerifyFull);
        result.ChannelBinding.Should().Be(ChannelBinding.Require);
    }

    [Fact]
    public void WithoutNeonUrl_UsesTheExistingConnectionString()
    {
        var configuration = Configure("ConnectionStrings:DefaultConnection", "Host=localhost;Database=synthetic");

        DatabaseConnection.Get(configuration).Should().Be("Host=localhost;Database=synthetic");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://user:password@example.test/database")]
    [InlineData("postgresql://user:password@example.test/database?sslmode=disable")]
    public void InvalidNeonUrl_FailsWithoutLeakingCredentials(string url)
    {
        var act = () => DatabaseConnection.Get(Configure("DATABASE_URL", url));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("DATABASE_URL must be a PostgreSQL URL with verified TLS.");
    }

    private static IConfiguration Configure(string key, string value) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
}
