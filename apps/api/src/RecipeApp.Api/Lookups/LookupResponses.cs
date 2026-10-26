namespace RecipeApp.Api.Lookups;

/// <summary>An owned category or department lookup value.</summary>
public sealed record LookupResponse(Guid Id, string Name);

/// <summary>A store belonging to a seeded supermarket chain.</summary>
public sealed record StoreResponse(Guid Id, string Code, string Name, int DisplayOrder);

/// <summary>A seeded supermarket chain and its stores.</summary>
public sealed record SupermarketChainResponse(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<StoreResponse> Stores);
