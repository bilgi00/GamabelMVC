using gamabelmvc.Api;

namespace gamabelmvc.Services.MobileApi;

public interface IMobileUserDirectoryService
{
    Task<IReadOnlyList<MobileDirectoryUserResponse>> GetUsersAsync(
        CancellationToken cancellationToken);
}
