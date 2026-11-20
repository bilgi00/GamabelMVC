using gamabelmvc.Api;
using gamabelmvc.Services;
using MySqlConnector;

namespace gamabelmvc.Services.Api2;

public sealed class BirimDirectoryService(
    DbConnectionFactory connectionFactory) : IBirimDirectoryService
{
    public async Task<IReadOnlyList<Api2BirimResponse>> GetBirimlerAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT `id`, `birim_adi`
            FROM `birimler`
            ORDER BY `birim_adi`, `id`
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var birimler = new List<Api2BirimResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            birimler.Add(new Api2BirimResponse(
                reader.GetInt32(0),
                reader.GetString(1)));
        }

        return birimler;
    }
}
