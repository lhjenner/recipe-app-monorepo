using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Lookups;

/// <summary>Payload for creating or retrieving an owned lookup value.</summary>
public sealed record CreateLookupRequest
{
    [Required, StringLength(100)]
    public required string Name { get; init; }
}
