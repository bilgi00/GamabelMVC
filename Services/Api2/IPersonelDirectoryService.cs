using gamabelmvc.Api;

namespace gamabelmvc.Services.Api2;

public interface IPersonelDirectoryService
{
    Task<IReadOnlyList<Api2PersonelResponse>> GetActivePersonellerAsync(
        CancellationToken cancellationToken);
}
