using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace gamabelmvc.Services.MobileApi;

internal static class MobileJwtSettings
{
    public const string Issuer = "gamabel-mobile-api";
    public const string Audience = "gamabel-mobile";
    private const string SigningKeyConfigurationPath = "MobileApi:Jwt:SigningKey";

    public static bool HasValidSigningKey(IConfiguration configuration)
    {
        var key = configuration[SigningKeyConfigurationPath];
        return !string.IsNullOrWhiteSpace(key) && Encoding.UTF8.GetByteCount(key) >= 32;
    }

    public static SymmetricSecurityKey GetSigningKey(IConfiguration configuration)
    {
        var key = configuration[SigningKeyConfigurationPath];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                $"{SigningKeyConfigurationPath} must be configured with at least 32 UTF-8 bytes.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
