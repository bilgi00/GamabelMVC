using MySqlConnector;

namespace gamabelmvc.Services;

public static class AyKilitKontrol
{
    public static async Task<bool> KilitliMiAsync(MySqlConnection connection, int yil, int ay)
    {
        if (yil < 1 || ay is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(ay), "Geçerli bir yıl ve ay belirtilmelidir.");

        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM ay_kilitleri WHERE yil = @yil AND ay = @ay",
            connection);
        command.Parameters.AddWithValue("@yil", yil);
        command.Parameters.AddWithValue("@ay", ay);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }
}
