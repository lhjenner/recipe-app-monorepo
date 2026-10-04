namespace RecipeApp.Api.Auth;

public class DuplicateEmailException() : Exception("A user with this email address already exists.");
