using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using gamabelmvc.Api;
using gamabelmvc.Services;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

namespace gamabelmvc.Services.MobileApi;

public sealed class MobileAuthService(
    DbConnectionFactory connectionFactory,
    IConfiguration configuration) : IMobileAuthService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task<MobileLoginResponse?> LoginAsync(
        MobileLoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(@"
            SELECT Id, SubeId, AdSoyad, KullaniciAdi, SifreHash, Rol, AktifMi
            FROM stk_Kullanici
            WHERE KullaniciAdi = @username
            LIMIT 1", connection);
        command.Parameters.AddWithValue("@username", username);

        int userId;
        int branchId;
        string fullName;
        string storedHash;
        string role;
        string storedUsername;
        bool isActive;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            userId = reader.GetInt32("Id");
            branchId = reader.GetInt32("SubeId");
            fullName = reader["AdSoyad"]?.ToString() ?? string.Empty;
            storedUsername = reader["KullaniciAdi"]?.ToString() ?? username;
            storedHash = reader["SifreHash"]?.ToString() ?? string.Empty;
            role = reader["Rol"]?.ToString() ?? string.Empty;
            isActive = reader["AktifMi"] != DBNull.Value && Convert.ToBoolean(reader["AktifMi"]);
        }

        var isBcryptHash = storedHash.StartsWith("$2", StringComparison.Ordinal);
        if (!isActive || !isBcryptHash || !BCrypt.Net.BCrypt.Verify(request.Password, storedHash))
            return null;

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(TokenLifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, storedUsername),
            new Claim(ClaimTypes.Role, role),
            new Claim("full_name", fullName),
            new Claim("branch_id", branchId.ToString(CultureInfo.InvariantCulture))
        };
        var credentials = new SigningCredentials(
            MobileJwtSettings.GetSigningKey(configuration),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: MobileJwtSettings.Issuer,
            audience: MobileJwtSettings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new MobileLoginResponse(
            accessToken,
            "Bearer",
            expiresAt,
            new MobileUserResponse(userId, storedUsername, fullName, role));
    }
}
