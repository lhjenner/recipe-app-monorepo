using System.Text.Json;
using FirestoreMigration;

namespace RecipeApp.Api.ComponentTests.Migration;

internal static class RecipeTestData
{
    public static string RecipeJson(Guid ingredientId, bool withImage = false,
        string title = "Synthetic recipe", string tag = "Dinner", string instruction = "Mix.") => $$$$"""
        [{
          "id":"synthetic-recipe",
          "title":"{{{{title}}}}",
          {{{{(withImage ? "\"imageUrl\":\"https://example.test/synthetic\"," : "")}}}}
          "tags":["{{{{tag}}}}"],
          "steps":[{
            "instructions":"{{{{instruction}}}}",
            "ingredients":[{"id":"{{{{ingredientId}}}}","name":"Synthetic flour","quantity":"1 cup"}]
          }]
        }]
        """;

    public static string RecipesJson(Guid availableIngredientId, Guid missingIngredientId) => $$$$"""
        [
          {"id":"valid-recipe","title":"Valid recipe","tags":[],"steps":[{
            "instructions":"Mix.","ingredients":[{
              "id":"{{{{availableIngredientId}}}}","name":"Synthetic flour"
            }]
          }]},
          {"id":"invalid-recipe","title":"Invalid recipe","tags":[],"steps":[{
            "instructions":"Mix.","ingredients":[{
              "id":"{{{{missingIngredientId}}}}","name":"Missing Ingredient"
            }]
          }]}
        ]
        """;

    public static async Task<RecipeMigrationReport> ReadReportAsync(string reportPath)
    {
        await using var stream = File.OpenRead(reportPath);
        return await JsonSerializer.DeserializeAsync<RecipeMigrationReport>(stream)
            ?? throw new InvalidOperationException("Recipe migration report could not be read.");
    }
}
