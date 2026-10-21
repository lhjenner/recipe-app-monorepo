namespace RecipeApp.Api.Lookups;

public interface ILookupService
{
    Task<IReadOnlyList<LookupResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<LookupResponse> CreateCategoryAsync(
        CreateLookupRequest request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupResponse>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<LookupResponse> CreateDepartmentAsync(
        CreateLookupRequest request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SupermarketChainResponse>> GetSupermarketsAsync(
        CancellationToken cancellationToken);
}
