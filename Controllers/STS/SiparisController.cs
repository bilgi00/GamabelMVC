using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Services;
using MySqlConnector;
using System.Text.Json;

namespace gamabelmvc.Controllers.STS;

public class SiparisController : Controller
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IConfiguration _configuration;

    public SiparisController(DbConnectionFactory dbFactory, ISystemInfoService systemInfoService, IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _systemInfoService = systemInfoService;
        _configuration = configuration;
    }

    // GET: Siparis/FabrikaListe - Fabrika siparişleri listesi + PDF çıktısı
    public async Task<IActionResult> FabrikaListe(string firma = "")
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var haftaNo = GetHaftaNo();
            var siparisler = new List<dynamic>();
            var firmalar = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                // Firmalar listesini çek
                var firmaQuery = @"
                    SELECT DISTINCT u.Firma
                    FROM stk_Urun u
                    WHERE u.Firma IS NOT NULL AND u.Firma != ''
                    ORDER BY u.Firma";

                using (var firmaCmd = new MySqlCommand(firmaQuery, conn))
                {
                    using (var reader = await firmaCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            firmalar.Add(new { Firma = reader.GetString("Firma") });
                        }
                    }
                }

                // Siparişleri çek (Firma filtreleme ile)
                var query = @"
                    SELECT fs.*, u.Ad as UrunAdi, u.Firma
                    FROM stk_FabrikaSiparisi fs
                    JOIN stk_Urun u ON fs.UrunId = u.Id
                    WHERE fs.HaftaNo = @HaftaNo";

                if (!string.IsNullOrWhiteSpace(firma) && firma != "Tüm Firmalar")
                {
                    query += " AND u.Firma = @Firma";
                }

                query += " ORDER BY fs.SiparisTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                    if (!string.IsNullOrWhiteSpace(firma) && firma != "Tüm Firmalar")
                    {
                        cmd.Parameters.AddWithValue("@Firma", firma);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            siparisler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                Firma = reader["Firma"] == DBNull.Value ? string.Empty : reader["Firma"]?.ToString() ?? string.Empty,
                                Miktar = reader.GetDecimal("Miktar"),
                                Durum = reader.GetString("Durum"),
                                SiparisTarihi = Convert.ToDateTime(reader["SiparisTarihi"]),
                                TahminiTeslimTarihi = reader["TahminiTeslimTarihi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["TahminiTeslimTarihi"]),
                                Not = reader["Not"] == DBNull.Value ? string.Empty : reader["Not"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            ViewBag.Firmalar = firmalar;
            ViewBag.SeciiliFirma = firma;
            return View("~/Views/STS/Siparis/FabrikaListe.cshtml", siparisler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SevkiyattanOlustur(int sevkiyatId)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                var detailQuery = @"
                    SELECT sv.Id, sv.Durum as SevkiyatDurumu, e.UrunId, e.Miktar, e.HaftaNo, u.Ad as UrunAdi
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    WHERE sv.Id = @Id";

                int urunId = 0;
                decimal miktar = 0;
                string haftaNo = string.Empty;
                string urunAdi = string.Empty;
                string sevkiyatDurumu = string.Empty;

                using (var detailCmd = new MySqlCommand(detailQuery, conn))
                {
                    detailCmd.Parameters.AddWithValue("@Id", sevkiyatId);
                    using (var reader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            TempData["Hata"] = "Sevkiyat kaydı bulunamadı.";
                            return RedirectToAction("Index", "Sevkiyat");
                        }

                        sevkiyatDurumu = reader.GetString("SevkiyatDurumu");
                        urunId = reader.GetInt32("UrunId");
                        miktar = reader.GetDecimal("Miktar");
                        haftaNo = reader.GetString("HaftaNo");
                        urunAdi = reader.GetString("UrunAdi");
                    }
                }

                if (sevkiyatDurumu != "Onaylandi")
                {
                    TempData["Hata"] = "Fabrikaya sipariş sadece şube tarafından onaylanan sevkiyatlar için oluşturulabilir.";
                    return RedirectToAction("Index", "Sevkiyat");
                }

                using (var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM stk_FabrikaSiparisi WHERE KaynakSevkiyatId = @KaynakSevkiyatId", conn))
                {
                    checkCmd.Parameters.AddWithValue("@KaynakSevkiyatId", sevkiyatId);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);
                    if (exists > 0)
                    {
                        TempData["Hata"] = "Bu sevkiyat için fabrika siparişi daha önce oluşturulmuş.";
                        return RedirectToAction("Index", "Sevkiyat");
                    }
                }

                var insertQuery = @"
                    INSERT INTO stk_FabrikaSiparisi (KaynakSevkiyatId, UrunId, Miktar, HaftaNo, SiparisTarihi, Durum, `Not`)
                    VALUES (@KaynakSevkiyatId, @UrunId, @Miktar, @HaftaNo, NOW(), '', @Not)";

                int siparisId = 0;
                using (var insertCmd = new MySqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@KaynakSevkiyatId", sevkiyatId);
                    insertCmd.Parameters.AddWithValue("@UrunId", urunId);
                    insertCmd.Parameters.AddWithValue("@Miktar", miktar);
                    insertCmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                    insertCmd.Parameters.AddWithValue("@Not", $"Sevkiyat sonrası otomatik sipariş önerisi: {urunAdi}");
                    await insertCmd.ExecuteNonQueryAsync();
                    
                    // Oluşturulan sipariş ID'sini al
                    using (var lastIdCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                    {
                        siparisId = Convert.ToInt32(await lastIdCmd.ExecuteScalarAsync() ?? 0);
                    }
                }

                // Sipariş oluşturma işlemini log et
                int kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
                if (siparisId > 0 && kullaniciId > 0)
                {
                    var yeniDegerler = new
                    {
                        UrunId = urunId,
                        Miktar = miktar,
                        HaftaNo = haftaNo,
                        SiparisTarihi = DateTime.Now,
                        Durum = "SiparisVerildi",
                        KaynakSevkiyatId = sevkiyatId,
                        Not = $"Sevkiyat sonrası otomatik sipariş önerisi: {urunAdi}"
                    };
                    await LogSiparisAuditAsync(siparisId, "Olustur", kullaniciId, yeniDegerler: yeniDegerler);
                }
            }

            TempData["Basari"] = "Fabrika siparişi oluşturuldu.";
            return RedirectToAction("Index", "Sevkiyat");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Fabrika siparişi oluşturulamadı: {ex.Message}";
            return RedirectToAction("Index", "Sevkiyat");
        }
    }

    [HttpPost]
    public async Task<IActionResult> EksikKaydidenSiparisTur(int eksikKaydiId)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                // Eksik kaydı verilerini al
                var detailQuery = @"
                    SELECT e.UrunId, e.Miktar, e.HaftaNo, u.Ad as UrunAdi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    WHERE e.Id = @Id";

                int urunId = 0;
                decimal miktar = 0;
                string haftaNo = string.Empty;
                string urunAdi = string.Empty;

                using (var detailCmd = new MySqlCommand(detailQuery, conn))
                {
                    detailCmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                    using (var reader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            TempData["Hata"] = "Eksik kaydı bulunamadı.";
                            return RedirectToAction("Index", "Sevkiyat");
                        }

                        urunId = reader.GetInt32("UrunId");
                        miktar = reader.GetDecimal("Miktar");
                        haftaNo = reader.GetString("HaftaNo");
                        urunAdi = reader.GetString("UrunAdi");
                    }
                }

                // Daha önce sipariş verilmiş mi kontrol et
                var checkQuery = @"
                    SELECT COUNT(*) FROM stk_FabrikaSiparisi 
                    WHERE UrunId = @UrunId AND HaftaNo = @HaftaNo";

                using (var checkCmd = new MySqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@UrunId", urunId);
                    checkCmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);
                    if (exists > 0)
                    {
                        TempData["Hata"] = "Bu ürün için bu haftada zaten fabrika siparişi oluşturulmuş.";
                        return RedirectToAction("Index", "Sevkiyat");
                    }
                }

                // Fabrika siparişi oluştur
                var insertQuery = @"
                    INSERT INTO stk_FabrikaSiparisi (UrunId, Miktar, HaftaNo, SiparisTarihi, Durum, `Not`)
                    VALUES (@UrunId, @Miktar, @HaftaNo, NOW(), '', @Not)";

                int siparisId = 0;
                using (var insertCmd = new MySqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@UrunId", urunId);
                    insertCmd.Parameters.AddWithValue("@Miktar", miktar);
                    insertCmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                    insertCmd.Parameters.AddWithValue("@Not", $"Eksik kaydından doğrudan sipariş: {urunAdi}");
                    await insertCmd.ExecuteNonQueryAsync();
                    
                    // Oluşturulan sipariş ID'sini al
                    using (var lastIdCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                    {
                        siparisId = Convert.ToInt32(await lastIdCmd.ExecuteScalarAsync() ?? 0);
                    }
                }

                // Sipariş oluşturma işlemini log et
                int kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
                if (siparisId > 0 && kullaniciId > 0)
                {
                    var yeniDegerler = new
                    {
                        UrunId = urunId,
                        Miktar = miktar,
                        HaftaNo = haftaNo,
                        SiparisTarihi = DateTime.Now,
                        Durum = "SiparisVerildi",
                        Not = $"Eksik kaydından doğrudan sipariş: {urunAdi}"
                    };
                    await LogSiparisAuditAsync(siparisId, "Olustur", kullaniciId, yeniDegerler: yeniDegerler);
                }
            }

            TempData["Basari"] = "Fabrika siparişi oluşturuldu.";
            return RedirectToAction("Index", "Sevkiyat");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Fabrika siparişi oluşturulamadı: {ex.Message}";
            return RedirectToAction("Index", "Sevkiyat");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DurumGuncelle(int id, string durum)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        var izinliDurumlar = new[] { "SiparisVerildi", "Uretimde", "Yolda", "TeslimAlindi" };
        if (!izinliDurumlar.Contains(durum))
        {
            TempData["Hata"] = "Geçersiz sipariş durumu.";
            return RedirectToAction("FabrikaListe");
        }

        var durumSirasi = new Dictionary<string, int>
        {
            ["SiparisVerildi"] = 1,
            ["Uretimde"] = 2,
            ["Yolda"] = 3,
            ["TeslimAlindi"] = 4
        };

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                string mevcutDurum = string.Empty;
                using (var getCmd = new MySqlCommand("SELECT Durum FROM stk_FabrikaSiparisi WHERE Id = @Id", conn))
                {
                    getCmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = await getCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            TempData["Hata"] = "Sipariş kaydı bulunamadı.";
                            return RedirectToAction("FabrikaListe");
                        }
                        var durumValue = reader["Durum"];
                        mevcutDurum = durumValue == DBNull.Value ? string.Empty : (durumValue?.ToString() ?? string.Empty);
                    }
                }

                if (string.IsNullOrWhiteSpace(mevcutDurum))
                {
                    TempData["Hata"] = "Mevcut sipariş durumu boş veya NULL.";
                    return RedirectToAction("FabrikaListe");
                }

                if (!durumSirasi.ContainsKey(mevcutDurum))
                {
                    TempData["Hata"] = "Mevcut sipariş durumu geçersiz.";
                    return RedirectToAction("FabrikaListe");
                }

                // Sadece bir sonraki adıma geçişe izin verilir. Aynı duruma tekrar set edilebilir.
                if (durum != mevcutDurum && durumSirasi[durum] != durumSirasi[mevcutDurum] + 1)
                {
                    TempData["Hata"] = $"Durum geçişi kural dışı. {mevcutDurum} durumundan yalnızca bir sonraki adıma geçebilirsiniz.";
                    return RedirectToAction("FabrikaListe");
                }

                using (var cmd = new MySqlCommand("UPDATE stk_FabrikaSiparisi SET Durum = @Durum WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Durum", durum);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Durum değişikliğini log et
                int kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
                if (durum != mevcutDurum && kullaniciId > 0)
                {
                    await LogSiparisAuditAsync(
                        id, 
                        "DurumDegistir", 
                        kullaniciId, 
                        eskiDegerler: new { Durum = mevcutDurum },
                        yeniDegerler: new { Durum = durum }
                    );
                }
            }

            TempData["Basari"] = "Sipariş durumu güncellendi.";
            return RedirectToAction("FabrikaListe");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Sipariş durumu güncellenemedi: {ex.Message}";
            return RedirectToAction("FabrikaListe");
        }
    }

    [HttpPost]
    public async Task<IActionResult> MiktarGuncelle(int id, decimal miktar)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        if (miktar <= 0)
        {
            TempData["Hata"] = "Miktar 0'dan büyük olmalıdır.";
            return RedirectToAction("FabrikaListe");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                // Kayıt var mı kontrol et
                using (var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM stk_FabrikaSiparisi WHERE Id = @Id", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    var count = (long?)await checkCmd.ExecuteScalarAsync() ?? 0;
                    if (count == 0)
                    {
                        TempData["Hata"] = "Sipariş kaydı bulunamadı.";
                        return RedirectToAction("FabrikaListe");
                    }
                }

                using (var cmd = new MySqlCommand("UPDATE stk_FabrikaSiparisi SET Miktar = @Miktar WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Miktar", miktar);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Miktar değişikliğini log et
                int kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
                if (kullaniciId > 0)
                {
                    await LogSiparisAuditAsync(
                        id,
                        "Guncelle",
                        kullaniciId,
                        yeniDegerler: new { Miktar = miktar }
                    );
                }
            }

            TempData["Basari"] = "Sipariş miktarı güncellendi.";
            return RedirectToAction("FabrikaListe");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Sipariş miktarı güncellenemedi: {ex.Message}";
            return RedirectToAction("FabrikaListe");
        }
    }

    // GET: Siparis/ExportPDF - PDF çıktısı oluştur
    public async Task<IActionResult> ExportPDF()
    {
        try
        {
            var haftaNo = GetHaftaNo();
            var content = new System.Text.StringBuilder();
            content.AppendLine("FABRIKA SİPARİŞ LİSTESİ");
            content.AppendLine($"Hafta: {haftaNo}");
            content.AppendLine($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
            content.AppendLine("=".PadRight(80, '='));
            content.AppendLine();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                var query = @"
                    SELECT fs.*, u.Ad as UrunAdi
                    FROM stk_FabrikaSiparisi fs
                    JOIN stk_Urun u ON fs.UrunId = u.Id
                    WHERE fs.HaftaNo = @HaftaNo
                    ORDER BY u.Ad";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var siparisTarihi = Convert.ToDateTime(reader["SiparisTarihi"]);
                            var sipariNo = $"{siparisTarihi:yyyyMMdd}-{reader.GetInt32("Id")}";
                            content.AppendLine($"Sipariş No: {sipariNo}");
                            content.AppendLine($"Ürün: {reader.GetString("UrunAdi")}");
                            content.AppendLine($"Miktar: {reader.GetDecimal("Miktar")}");
                            content.AppendLine($"Durum: {reader.GetString("Durum")}");
                            content.AppendLine($"Tahmini Teslim: {(reader["TahminiTeslimTarihi"] == DBNull.Value ? "Bilgisiz" : Convert.ToDateTime(reader["TahminiTeslimTarihi"]).ToString("dd.MM.yyyy"))}");
                            content.AppendLine("-".PadRight(80, '-'));
                        }
                    }
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(content.ToString());
            return File(bytes, "application/pdf", $"FabrikaSiparisleri_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Siparis/OzetSiparis - Firma siparişlerinin özeti
    public async Task<IActionResult> OzetSiparis(string firma = "")
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return Unauthorized();

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisColumnsAsync(conn);

                var query = @"
                    SELECT u.Ad as UrunAdi, SUM(fs.Miktar) as ToplamMiktar
                    FROM stk_FabrikaSiparisi fs
                    JOIN stk_Urun u ON fs.UrunId = u.Id
                    WHERE 1=1";

                if (!string.IsNullOrWhiteSpace(firma))
                {
                    query += " AND u.Firma = @Firma";
                }

                query += " GROUP BY u.Ad ORDER BY u.Ad";

                var ozetler = new List<dynamic>();

                using (var cmd = new MySqlCommand(query, conn))
                {
                    if (!string.IsNullOrWhiteSpace(firma))
                    {
                        cmd.Parameters.AddWithValue("@Firma", firma);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ozetler.Add(new
                            {
                                UrunAdi = reader.GetString("UrunAdi"),
                                ToplamMiktar = reader.GetDecimal("ToplamMiktar")
                            });
                        }
                    }
                }

                return Json(ozetler);
            }
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    private string GetHaftaNo()
    {
        var today = DateTime.Now;
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var weekOfYear = cultureInfo.Calendar.GetWeekOfYear(today, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
        return $"{today.Year}-W{weekOfYear:D2}";
    }

    private async Task EnsureSiparisColumnsAsync(MySqlConnection conn)
    {
        var tableQuery = @"
            CREATE TABLE IF NOT EXISTS stk_FabrikaSiparisi (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                KaynakSevkiyatId INT NULL,
                UrunId INT NOT NULL,
                Miktar DECIMAL(10,2) NOT NULL,
                HaftaNo VARCHAR(20) NOT NULL,
                SiparisTarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                Durum VARCHAR(50) NOT NULL DEFAULT 'SiparisVerildi',
                TahminiTeslimTarihi DATETIME NULL,
                `Not` VARCHAR(500) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;";

        using (var tableCmd = new MySqlCommand(tableQuery, conn))
        {
            await tableCmd.ExecuteNonQueryAsync();
        }

        var columnQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_FabrikaSiparisi'
              AND COLUMN_NAME = 'KaynakSevkiyatId';";

        using (var checkCmd = new MySqlCommand(columnQuery, conn))
        {
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var alterCmd = new MySqlCommand("ALTER TABLE stk_FabrikaSiparisi ADD COLUMN KaynakSevkiyatId INT NULL AFTER Id;", conn))
                {
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }

        var indexQuery = @"
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_FabrikaSiparisi'
              AND INDEX_NAME = 'uq_stk_fabrikasiparisi_kaynaksevkiyat';";

        using (var indexCmd = new MySqlCommand(indexQuery, conn))
        {
            var exists = Convert.ToInt32(await indexCmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var createCmd = new MySqlCommand("CREATE UNIQUE INDEX uq_stk_fabrikasiparisi_kaynaksevkiyat ON stk_FabrikaSiparisi(KaynakSevkiyatId);", conn))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    /// <summary>
    /// Sipariş işlemlerini denetim günlüğüne kaydet (MAC, IP, Bilgisayar Adı ile)
    /// </summary>
    private async Task LogSiparisAuditAsync(
        int siparisId, 
        string islemTipi, 
        int yapanKullaniciId, 
        object? eskiDegerler = null, 
        object? yeniDegerler = null,
        string notlar = "")
    {
        try
        {
            var ipAdresi = _systemInfoService.GetClientIpAddress(HttpContext);
            var bilgisayarAdi = _systemInfoService.GetComputerName();
            var macAdresi = _systemInfoService.GetMacAddress();

            var connectionString = _configuration.GetConnectionString("MyConnection");
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO stk_SiparisAuditLog 
                (SiparisId, IslemTipi, YapanKullaniciId, IslemTarihi, IpAdresi, BilgisayarAdi, MacAdresi, EskiDegerler, YeniDegerler, Notlar)
                VALUES 
                (@SiparisId, @IslemTipi, @YapanKullaniciId, NOW(), @IpAdresi, @BilgisayarAdi, @MacAdresi, @EskiDegerler, @YeniDegerler, @Notlar)";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@SiparisId", siparisId);
            command.Parameters.AddWithValue("@IslemTipi", islemTipi);
            command.Parameters.AddWithValue("@YapanKullaniciId", yapanKullaniciId);
            command.Parameters.AddWithValue("@IpAdresi", ipAdresi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BilgisayarAdi", bilgisayarAdi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@MacAdresi", macAdresi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@EskiDegerler", eskiDegerler != null ? JsonSerializer.Serialize(eskiDegerler) : (object)DBNull.Value);
            command.Parameters.AddWithValue("@YeniDegerler", yeniDegerler != null ? JsonSerializer.Serialize(yeniDegerler) : (object)DBNull.Value);
            command.Parameters.AddWithValue("@Notlar", string.IsNullOrEmpty(notlar) ? (object)DBNull.Value : notlar);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Log kaydı başarısız olsa bile, ana işlemi etkilemesin
            System.Diagnostics.Debug.WriteLine($"Sipariş denetim günlüğü kaydetme hatası: {ex.Message}");
        }
    }
}

