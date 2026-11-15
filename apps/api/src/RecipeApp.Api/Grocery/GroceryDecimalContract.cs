namespace RecipeApp.Api.Grocery;

public static class GroceryDecimalContract
{
    public const int Precision = 28;
    public const int Scale = 9;
    public const decimal MaximumStoredAmount = 9_999_999_999_999_999_999.999999999m;
    public const decimal MaximumEnteredPurchaseQuantity = 9_999m;
    public const int MaximumBatchItemCount = 10;
    public const int MaximumNameLength = 200;
    public const int MaximumNoteLength = 250;

    public static bool CanRepresent(decimal amount) =>
        amount >= 0
        && amount <= MaximumStoredAmount
        && decimal.Round(amount, Scale) == amount;
}
