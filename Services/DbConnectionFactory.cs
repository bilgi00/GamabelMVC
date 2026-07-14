using MySqlConnector;

namespace gamabelmvc.Services;

public class DbConnectionFactory
{
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;
    private static bool _migrationExecuted = false;

    public DbConnectionFactory(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _environment = environment;
        _connectionString = configuration.GetConnectionString("MyConnection") ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<MySqlConnection> CreateConnectionAsync()
    {
        try
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Veritabanı bağlantısı başarısız. Ortam: {_environment.EnvironmentName}. Hata: {ex.Message}", ex);
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        if (_migrationExecuted) return;

        try
        {
            await using var connection = await CreateConnectionAsync();

            // Firma alanını ekle (varsa atla)
            string addColumnQuery = @"
                ALTER TABLE stk_Urun 
                ADD COLUMN IF NOT EXISTS Firma VARCHAR(255) NULL AFTER Birim;";

            try
            {
                await using var cmd = new MySqlCommand(addColumnQuery, connection);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* Alan zaten mevcut */ }

            // İndeks oluştur (varsa atla)
            string createIndexQuery = @"
                CREATE INDEX IF NOT EXISTS idx_firma ON stk_Urun(Firma);";

            try
            {
                await using var cmd = new MySqlCommand(createIndexQuery, connection);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* İndeks zaten mevcut */ }

            _migrationExecuted = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Veritabanı migration hatası: {ex.Message}");
        }
    }
}
