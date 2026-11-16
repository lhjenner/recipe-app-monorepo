using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryConcurrentCommandSteps(ApiAuthContext context)
{
    private int[] _statuses = [];
    private int[] _expected = [];
    private Guid _commandId;
    private Guid _ownerId;
    private string _connectionString = string.Empty;
    private string _operation = string.Empty;

    [When("two overlapping {string} commands are submitted with {string} content")]
    public async Task SubmitOverlappingAsync(string operation, string content)
    {
        _operation = operation;
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        _ownerId = (await db.Users.SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant())).Id;
        var owner = new TestCurrentUser(_ownerId);
        var normalFactory = new TestDatabaseFactory(_connectionString);
        var initial = new GroceryManualEntryService(normalFactory, owner, TimeProvider.System);
        if (operation != "first add")
        {
            var seed = await initial.AddManualItemsAsync(
                new GroceryManualEntryCommand(Guid.NewGuid(), 0,
                    [new("Apples", null, 1, "item"), new("Pears", null, 1, "item")], []),
                CancellationToken.None);
            seed.StatusCode.Should().Be(201);
        }

        var list = await db.GroceryLists.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == _ownerId);
        var items = list is null ? [] : await db.GroceryItems.AsNoTracking()
            .Where(value => value.GroceryListId == list.Id).OrderBy(value => value.NameSnapshot).ToArrayAsync();
        var version = list?.Version ?? 0;
        _commandId = Guid.NewGuid();
        var different = content == "different";
        var gate = new FirstSaveBarrier();
        var factory = new TestDatabaseFactory(_connectionString, gate);

        if (operation is "first add" or "existing add")
        {
            var service = new GroceryManualEntryService(factory, owner, TimeProvider.System);
            var first = new GroceryManualEntryCommand(_commandId, version, [new("Oats", "first", 1, "item")], []);
            var second = different ? first with { Items = [new("Oats", "second", 1, "item")] } : first;
            var outcomes = await Task.WhenAll(
                service.AddManualItemsAsync(first, CancellationToken.None),
                service.AddManualItemsAsync(second, CancellationToken.None));
            _statuses = outcomes.Select(value => value.StatusCode).ToArray();
            if (!different)
            {
                outcomes[0].Receipt.Should().BeEquivalentTo(outcomes[1].Receipt);
            }
            _expected = different ? [201, 409] : [201, 201];
        }
        else if (operation == "update")
        {
            var service = new GroceryItemMutationService(factory, owner, TimeProvider.System);
            var first = new GroceryItemUpdateRequest(_commandId, version, 2, "item", "first", null, null, null);
            var second = different ? first with { Note = "second" } : first;
            var outcomes = await Task.WhenAll(
                service.UpdateItemAsync(items[0].Id, first, CancellationToken.None),
                service.UpdateItemAsync(items[0].Id, second, CancellationToken.None));
            _statuses = outcomes.Select(value => value.StatusCode).ToArray();
            if (!different)
            {
                outcomes[0].Receipt.Should().BeEquivalentTo(outcomes[1].Receipt);
            }
            _expected = different ? [200, 409] : [200, 200];
        }
        else
        {
            var service = new GroceryItemCancellationService(factory, owner, TimeProvider.System);
            var command = new GroceryCommandMetadata(_commandId, version);
            var outcomes = await Task.WhenAll(
                service.CancelItemAsync(items[0].Id, command, CancellationToken.None),
                service.CancelItemAsync(items[different ? 1 : 0].Id, command, CancellationToken.None));
            _statuses = outcomes.Select(value => value.StatusCode).ToArray();
            _expected = different ? [204, 409] : [204, 204];
        }
    }

    [Then("the overlapping grocery commands have the expected outcomes without duplicate writes")]
    public async Task AssertOutcomesAsync()
    {
        _statuses.Should().BeEquivalentTo(_expected);
        await using var db = new TestDatabaseFactory(_connectionString).CreateDbContext();
        (await db.GroceryCommandReceipts.CountAsync(value =>
            value.OwnerUserId == _ownerId && value.CommandId == _commandId)).Should().Be(1);
        var items = await db.GroceryItems.Where(value => value.OwnerUserId == _ownerId).ToArrayAsync();
        if (_operation is "first add" or "existing add")
        {
            items.Should().ContainSingle(value => value.NameSnapshot == "Oats")
                .Which.OutstandingPurchaseQuantity.Should().Be(1);
        }
        else if (_operation == "update")
        {
            items.Single(value => value.NameSnapshot == "Apples").OutstandingPurchaseQuantity.Should().Be(2);
        }
        else
        {
            items.Count(value => value.State == GroceryItemState.Cancelled).Should().Be(1);
        }
    }

    private sealed record TestCurrentUser(Guid UserId) : ICurrentUser;

    private sealed class TestDatabaseFactory(string connectionString, FirstSaveBarrier? barrier = null)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
            if (barrier is not null)
            {
                options.AddInterceptors(barrier);
            }
            return new AppDbContext(options.Options);
        }
    }

    private sealed class FirstSaveBarrier : SaveChangesInterceptor
    {
        private readonly HashSet<DbContext> _seen = [];
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var wait = false;
            lock (_seen)
            {
                if (_seen.Add(eventData.Context!))
                {
                    wait = true;
                    if (_seen.Count == 2)
                    {
                        _ready.TrySetResult();
                    }
                }
            }
            if (wait)
            {
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
