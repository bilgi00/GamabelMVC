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

            string ensurePaymentStatusQuery = @"
                ALTER TABLE prs_ot_acik_faturalar
                ADD COLUMN IF NOT EXISTS odeme_durumu VARCHAR(20) NOT NULL DEFAULT 'bekliyor' AFTER odemeye_dahil_edildi;

                UPDATE prs_ot_acik_faturalar
                SET odeme_durumu = CASE WHEN odemeye_dahil_edildi = 1 THEN 'odendi' ELSE 'bekliyor' END
                WHERE odeme_durumu IS NULL OR odeme_durumu = '';

                CREATE INDEX IF NOT EXISTS idx_ot_acik_faturalar_durum ON prs_ot_acik_faturalar(odeme_durumu, import_batch_id);
            ";

            await using (var cmd = new MySqlCommand(ensurePaymentStatusQuery, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            string createRoleTableQuery = @"
                CREATE TABLE IF NOT EXISTS roll (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    ad VARCHAR(100) NOT NULL UNIQUE,
                    aciklama VARCHAR(255),
                    aktif_mi TINYINT(1) DEFAULT 1,
                    menu_kullanici_yonetimi TINYINT(1) DEFAULT 0,
                    menu_personel TINYINT(1) DEFAULT 0,
                    menu_puantaj TINYINT(1) DEFAULT 0,
                    menu_rapor TINYINT(1) DEFAULT 0,
                    menu_mesai TINYINT(1) DEFAULT 0,
                    menu_tatiller TINYINT(1) DEFAULT 0,
                    menu_odeme_talimat TINYINT(1) DEFAULT 0,
                    menu_firma_yonetimi TINYINT(1) DEFAULT 0,
                    menu_banka_yonetimi TINYINT(1) DEFAULT 0,
                    menu_yetkilendirme TINYINT(1) DEFAULT 0,
                    menu_dokumantasyon TINYINT(1) DEFAULT 0,
                    menu_sikayet_admin TINYINT(1) DEFAULT 0,
                    olusturma_tarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
                    guncelleme_tarihi DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using (var cmd = new MySqlCommand(createRoleTableQuery, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            string seedRolesQuery = @"
                INSERT INTO roll (ad, aciklama, aktif_mi, menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai, menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi, menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
                SELECT 'admin', 'Sistem yöneticisi', 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
                WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'admin');

                INSERT INTO roll (ad, aciklama, aktif_mi, menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai, menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi, menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
                SELECT 'birim_amiri', 'Birim amiri', 1, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0
                WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'birim_amiri');

                INSERT INTO roll (ad, aciklama, aktif_mi, menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai, menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi, menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
                SELECT 'sube_personeli', 'Şube personeli', 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'sube_personeli');

                INSERT INTO roll (ad, aciklama, aktif_mi, menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai, menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi, menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
                SELECT 'depo_sorumlusu', 'Depo sorumlusu', 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'depo_sorumlusu');";

            await using (var cmd = new MySqlCommand(seedRolesQuery, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            _migrationExecuted = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Veritabanı migration hatası: {ex.Message}");
        }
    }
}
