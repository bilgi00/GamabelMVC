using System.Globalization;
using gamabelmvc.Api;
using gamabelmvc.Services;
using MySqlConnector;

namespace gamabelmvc.Services.Api2;

public sealed class PersonelDirectoryService(
    DbConnectionFactory connectionFactory) : IPersonelDirectoryService
{
    public async Task<IReadOnlyList<Api2PersonelResponse>> GetActivePersonellerAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT `per_no`, `ad`, `soyad`, `per_statu`, `birim_adi`
            FROM `personeller`
            WHERE `aktif_mi` = 1
            ORDER BY `birim_adi`, `ad`, `soyad`
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var personeller = new List<Api2PersonelResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            personeller.Add(new Api2PersonelResponse(
                ReadValue(reader, 0),
                ReadValue(reader, 1),
                ReadValue(reader, 2),
                ReadValue(reader, 3),
                ReadValue(reader, 4)));
        }

        return personeller;
    }

    private static string ReadValue(MySqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
                ?? string.Empty;
}
