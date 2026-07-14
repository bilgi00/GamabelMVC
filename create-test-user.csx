#r "nuget: MySqlConnector, 6.0.0"

using MySqlConnector;

var connectionString = "Server=localhost;Database=u2636310_dbE97;User=u2636310_userE97;Password=VQLjJ1hmp3VdOQTp;SslMode=Preferred;";

using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

// Create test user
using var cmd = new MySqlCommand(
    "INSERT IGNORE INTO admin_kullanicilar (kullanici_adi, sifre, birim, rol) VALUES (@user, @pass, @birim, @rol)",
    connection);

cmd.Parameters.AddWithValue("@user", "test");
cmd.Parameters.AddWithValue("@pass", "test");
cmd.Parameters.AddWithValue("@birim", "AÇIK PAZAR");
cmd.Parameters.AddWithValue("@rol", "birim_amiri");

var result = await cmd.ExecuteNonQueryAsync();
Console.WriteLine($"User creation result: {result} rows affected");

// Verify
using var verifyCmd = new MySqlCommand("SELECT id, kullanici_adi, birim FROM admin_kullanicilar WHERE kullanici_adi = 'test'", connection);
using var reader = await verifyCmd.ExecuteReaderAsync();
if (await reader.ReadAsync())
{
    Console.WriteLine($"✓ User created: ID={reader.GetInt32(0)}, User={reader.GetString(1)}, Birim={reader.GetString(2)}");
}
else
{
    Console.WriteLine("✗ User not found after creation");
}

await connection.CloseAsync();
