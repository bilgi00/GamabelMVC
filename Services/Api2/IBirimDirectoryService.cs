using gamabelmvc.Api;

namespace gamabelmvc.Services.Api2;

public interface IBirimDirectoryService
{
    Task<IReadOnlyList<Api2BirimResponse>> GetBirimlerAsync(
        CancellationToken cancellationToken);
}
