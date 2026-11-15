namespace RecipeApp.Api.Grocery;

public enum QuantityDimension
{
    Count,
    Mass,
    Volume
}

public enum GroceryItemState
{
    Outstanding,
    Completed,
    Cancelled
}

public enum GroceryContributionOrigin
{
    Manual,
    Recipe
}

public enum GroceryContributionState
{
    Outstanding,
    Completed,
    Cancelled
}

public enum GroceryPurchaseEventKind
{
    Purchase,
    Reversal
}

public enum GroceryPurchaseOrigin
{
    Manual,
    OnlineCart
}
