using gamabelmvc.Api;

namespace gamabelmvc.Services.MobileApi;

public interface IMobileInventoryService
{
    Task<IReadOnlyList<MobileCountResponse>> GetCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileProductResponse>> GetProductsAsync(CancellationToken cancellationToken);
    Task<MobileProductResponse?> CreateProductAsync(
        MobileCreateProductRequest request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileSystemStockResponse>?> GetSystemStockAsync(
        int countId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileMovementResponse>?> GetMovementsAsync(
        int countId,
        CancellationToken cancellationToken);
    Task<MobileMovementSaveResult> CreateMovementAsync(
        int countId,
        Guid uuid,
        MobileCreateMovementRequest request,
        string username,
        CancellationToken cancellationToken);
}
