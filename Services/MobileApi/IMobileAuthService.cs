using gamabelmvc.Api;

namespace gamabelmvc.Services.MobileApi;

public interface IMobileAuthService
{
    Task<MobileLoginResponse?> LoginAsync(MobileLoginRequest request, CancellationToken cancellationToken);
}
