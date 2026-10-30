namespace RecipeApp.Api.Images;

public sealed class ImageStorageUnavailableException(Exception innerException)
    : Exception("The private recipe image could not be retrieved or verified.", innerException);
