using System.Globalization;
using gamabelmvc.Api;
using gamabelmvc.Services;
using MySqlConnector;

namespace gamabelmvc.Services.MobileApi;

public sealed class MobileUserDirectoryService(
    DbConnectionFactory connectionFactory) : IMobileUserDirectoryService
{
    public async Task<IReadOnlyList<MobileDirectoryUserResponse>> GetUsersAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT `Id`, `KullaniciAdi`, `AdSoyad`, `AktifMi`
            FROM `stok_sayim_kullanicilari`
            ORDER BY `AdSoyad`, `KullaniciAdi`
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<MobileDirectoryUserResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new MobileDirectoryUserResponse(
                reader.GetInt32("Id"),
                reader.GetString("KullaniciAdi"),
                reader.GetString("AdSoyad"),
                Convert.ToBoolean(reader["AktifMi"], CultureInfo.InvariantCulture)));
        }

        return users;
    }
}
