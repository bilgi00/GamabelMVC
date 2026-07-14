using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Models.STS;
using gamabelmvc.Services;
using MySqlConnector;
using System.Xml.Linq;
using System.Text.Json;

namespace gamabelmvc.Controllers.STS
{
    public class EksikController : Controller
    {
        // Toplu kayıt için istek modeli
        public class EksikTopluSatirlarRequest
        {
            public List<EksikTopluSatir> satirlar { get; set; } = new();
        }

        private sealed class BekleyenSevkKontrolSonucu
        {
            public bool VarMi { get; set; }
            public string UrunAdi { get; set; } = string.Empty;
        }
        private readonly DbConnectionFactory _dbFactory;

        // AJAX: Ürün adı ile arama (autocomplete)
        [HttpGet]
        public async Task<IActionResult> UrunAra(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());
            using var conn = await _dbFactory.CreateConnectionAsync();
            var urunler = new List<object>();
            using (var cmd = new MySqlCommand("SELECT Id, Ad, Barkod, Birim FROM stk_Urun WHERE AktifMi = true AND Ad LIKE @q ORDER BY Ad LIMIT 10", conn))
            {
                cmd.Parameters.AddWithValue("@q", "%" + query + "%");
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    urunler.Add(new {
                        id = reader.GetInt32("Id"),
                        ad = reader.GetString("Ad"),
                        barkod = reader["Barkod"] == DBNull.Value ? null : reader["Barkod"].ToString(),
                        birim = reader.GetString("Birim")
                    });
                }
            }
            return Json(urunler);
        }

        // AJAX: Barkod ile ürün arama
        [HttpGet]
        public async Task<IActionResult> BarkodAra(string barkod)
        {
            if (string.IsNullOrWhiteSpace(barkod)) return Json(null);
            using var conn = await _dbFactory.CreateConnectionAsync();
            using (var cmd = new MySqlCommand("SELECT Id, Ad, Barkod, Birim FROM stk_Urun WHERE AktifMi = true AND Barkod = @b LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@b", barkod);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Json(new {
                        id = reader.GetInt32("Id"),
                        ad = reader.GetString("Ad"),
                        barkod = reader["Barkod"] == DBNull.Value ? null : reader["Barkod"].ToString(),
                        birim = reader.GetString("Birim")
                    });
                }
            }
            return Json(null);
        }



        public class EksikTopluSatir
        {
            public int UrunId { get; set; }
            public decimal Miktar { get; set; }
            public string? Aciklama { get; set; }
        }

        public EksikController(DbConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

    // GET: Eksik/SubeListe - Şubenin kendi eksiklerini listele
    public async Task<IActionResult> SubeListe()
    {
        try
        {
            var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
            if (subeId == 0) return RedirectToAction("Login", "Account");

            var eksikler = new List<StsEksikKaydi>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureEksikColumnsAsync(conn);

                var query = @"
                    SELECT e.*, u.Ad as UrunAdi, s.Ad as SubeAdi,
                           (
                               SELECT COUNT(DISTINCT e2.HaftaNo)
                               FROM stk_EksikKaydi e2
                               WHERE e2.SubeId = e.SubeId
                                 AND e2.UrunId = e.UrunId
                                 AND e2.GirisTarihi >= DATE_SUB(NOW(), INTERVAL 21 DAY)
                           ) as TekrarHaftaSayisi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                                        WHERE e.SubeId = @SubeId
                                            AND e.GirisTarihi >= DATE_SUB(NOW(), INTERVAL 15 DAY)
                    ORDER BY e.AcilMi DESC, e.GirisTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SubeId", subeId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            eksikler.Add(new StsEksikKaydi
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? null : reader["SiparisNo"]?.ToString(),
                                SubeId = reader.GetInt32("SubeId"),
                                UrunId = reader.GetInt32("UrunId"),
                                Urun = new StsUrun
                                {
                                    Id = reader.GetInt32("UrunId"),
                                    Ad = reader.GetString("UrunAdi")
                                },
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader.GetBoolean("AcilMi"),
                                Not = reader["Aciklama"] == DBNull.Value ? null : reader["Aciklama"]?.ToString(),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                TekrarHaftaSayisi = reader["TekrarHaftaSayisi"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TekrarHaftaSayisi"]),
                                TekrarliMi = reader["TekrarHaftaSayisi"] != DBNull.Value && Convert.ToInt32(reader["TekrarHaftaSayisi"]) >= 3,
                                Durum = reader.GetString("Durum"),
                                HaftaNo = reader.GetString("HaftaNo"),
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"])
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/Eksik/SubeListe.cshtml", eksikler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Eksik/Gecmis - Şubenin tüm eksik geçmişi
    public async Task<IActionResult> Gecmis()
    {
        try
        {
            var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
            if (subeId == 0) return RedirectToAction("Login", "Account");

            var eksikler = new List<StsEksikKaydi>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureEksikColumnsAsync(conn);

                var query = @"
                    SELECT e.*, u.Ad as UrunAdi,
                           (
                               SELECT COUNT(DISTINCT e2.HaftaNo)
                               FROM stk_EksikKaydi e2
                               WHERE e2.SubeId = e.SubeId
                                 AND e2.UrunId = e.UrunId
                                 AND e2.GirisTarihi >= DATE_SUB(e.GirisTarihi, INTERVAL 21 DAY)
                           ) as TekrarHaftaSayisi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    WHERE e.SubeId = @SubeId
                    ORDER BY e.GirisTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SubeId", subeId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            eksikler.Add(new StsEksikKaydi
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? null : reader["SiparisNo"]?.ToString(),
                                SubeId = reader.GetInt32("SubeId"),
                                UrunId = reader.GetInt32("UrunId"),
                                Urun = new StsUrun
                                {
                                    Id = reader.GetInt32("UrunId"),
                                    Ad = reader.GetString("UrunAdi")
                                },
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader.GetBoolean("AcilMi"),
                                Not = reader["Aciklama"] == DBNull.Value ? null : reader["Aciklama"]?.ToString(),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                TekrarHaftaSayisi = reader["TekrarHaftaSayisi"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TekrarHaftaSayisi"]),
                                TekrarliMi = reader["TekrarHaftaSayisi"] != DBNull.Value && Convert.ToInt32(reader["TekrarHaftaSayisi"]) >= 3,
                                Durum = reader.GetString("Durum"),
                                HaftaNo = reader.GetString("HaftaNo"),
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"])
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/Eksik/Gecmis.cshtml", eksikler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Eksik/Ekle - Yeni eksik kaydı ekleme formu
    public async Task<IActionResult> Ekle()
    {
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        if (subeId == 0) return RedirectToAction("Login", "Account");

        using (var conn = await _dbFactory.CreateConnectionAsync())
        {
            await EnsureEksikColumnsAsync(conn);

            // Ürünleri listele
            var urunler = new List<StsUrun>();
            using (var cmd = new MySqlCommand("SELECT Id, Ad, Barkod, Birim FROM stk_Urun WHERE AktifMi = true ORDER BY Ad", conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        urunler.Add(new StsUrun
                        {
                            Id = reader.GetInt32("Id"),
                            Ad = reader.GetString("Ad"),
                            Barkod = reader["Barkod"] == DBNull.Value ? null : reader["Barkod"]?.ToString(),
                            Birim = reader.GetString("Birim")
                        });
                    }
                }
            }
            ViewBag.Urunler = urunler;
        }

        return View("~/Views/STS/Eksik/Ekle.cshtml");
    }

    // POST: Eksik/Ekle
    [HttpPost]
    public async Task<IActionResult> Ekle([FromBody] EksikTopluSatirlarRequest request)
    {
        // DEBUG: Gelen JSON'u logla
        string satirlarJson = null;
        try {
            System.IO.Directory.CreateDirectory("E:/gamabelmvc/temp");
            satirlarJson = System.Text.Json.JsonSerializer.Serialize(request?.satirlar ?? new List<EksikTopluSatir>());
            System.IO.File.WriteAllText("E:/gamabelmvc/temp/satirlarJson.txt", satirlarJson ?? "<null>");
        } catch { }
        try
        {
            var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
            var kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
            if (subeId == 0) return Unauthorized(new { success = false, message = "Oturum süresi doldu. Lütfen tekrar giriş yapın." });

            var haftaNo = GetHaftaNo();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureEksikColumnsAsync(conn);

                var satirlar = request?.satirlar ?? new List<EksikTopluSatir>();
                if (satirlar.Count == 0)
                {
                    return BadRequest(new { success = false, message = "Lütfen en az bir ürün satırı ekleyiniz." });
                }

                var eklendi = 0;
                var atlandi = 0;
                var gecersizSatir = 0;
                var kopyaSatir = 0;
                var bekleyenSevkEngeli = 0;
                var engellenenUrunler = new List<string>();
                foreach (var satir in satirlar)
                {
                    if (satir.UrunId <= 0 || satir.Miktar <= 0)
                    {
                        atlandi++;
                        gecersizSatir++;
                        continue;
                    }

                    var bekleyenSevkSonucu = await BekleyenSevkiyatVarMiAsync(conn, subeId, satir.UrunId);
                    if (bekleyenSevkSonucu.VarMi)
                    {
                        atlandi++;
                        bekleyenSevkEngeli++;
                        var urunAdi = string.IsNullOrWhiteSpace(bekleyenSevkSonucu.UrunAdi)
                            ? $"ÜrünId:{satir.UrunId}"
                            : bekleyenSevkSonucu.UrunAdi;
                        if (!engellenenUrunler.Contains(urunAdi, StringComparer.OrdinalIgnoreCase))
                        {
                            engellenenUrunler.Add(urunAdi);
                        }
                        continue;
                    }

                    var checkQueryToplu = "SELECT COUNT(*) FROM stk_EksikKaydi WHERE HaftaNo = @HaftaNo AND SubeId = @SubeId AND UrunId = @UrunId";
                    using (var checkCmd = new MySqlCommand(checkQueryToplu, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                        checkCmd.Parameters.AddWithValue("@SubeId", subeId);
                        checkCmd.Parameters.AddWithValue("@UrunId", satir.UrunId);
                        var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync() ?? 0L);
                        if (count > 0)
                        {
                            atlandi++;
                            kopyaSatir++;
                            continue;
                        }
                    }

                    var siparisNo = await GenerateEksikSiparisNoAsync(conn, DateTime.Now);
                    var insertQueryToplu = @"
                        INSERT INTO stk_EksikKaydi (SiparisNo, SubeId, UrunId, Miktar, AcilMi, Aciklama, GeciktiMi, Durum, HaftaNo, GirisTarihi, GirisiYapanKullaniciId)
                        VALUES (@SiparisNo, @SubeId, @UrunId, @Miktar, @AcilMi, @Aciklama, @GeciktiMi, @Durum, @HaftaNo, NOW(), @GirisiYapanKullaniciId)";

                    using (var insertCmd = new MySqlCommand(insertQueryToplu, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@SiparisNo", siparisNo);
                        insertCmd.Parameters.AddWithValue("@SubeId", subeId);
                        insertCmd.Parameters.AddWithValue("@UrunId", satir.UrunId);
                        insertCmd.Parameters.AddWithValue("@Miktar", satir.Miktar);
                        insertCmd.Parameters.AddWithValue("@AcilMi", false);
                        insertCmd.Parameters.AddWithValue("@Aciklama", satir.Aciklama ?? "");
                        insertCmd.Parameters.AddWithValue("@GeciktiMi", IsLateEntry(DateTime.Now));
                        insertCmd.Parameters.AddWithValue("@Durum", "Bekliyor");
                        insertCmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                        insertCmd.Parameters.AddWithValue("@GirisiYapanKullaniciId", kullaniciId);
                        try
                        {
                            await insertCmd.ExecuteNonQueryAsync();
                            eklendi++;
                        }
                        catch (MySqlException ex) when (ex.Message.Contains("Miktar") || ex.Message.Contains("CHECK"))
                        {
                            atlandi++;
                            gecersizSatir++;
                            continue;
                        }
                    }

                    await CreateEksikNotificationsAsync(conn, subeId, satir.UrunId, satir.Aciklama, IsLateEntry(DateTime.Now));
                }

                var warningMessage = bekleyenSevkEngeli > 0
                    ? $"Sevk onayı bekleyen sevkiyat olduğundan {bekleyenSevkEngeli} satır eklenemedi."
                    : string.Empty;

                return Json(new
                {
                    success = true,
                    eklendi,
                    atlandi,
                    gecersizSatir,
                    kopyaSatir,
                    bekleyenSevkEngeli,
                    engellenenUrunler,
                    warningMessage,
                    message = $"Satır eklendi: {eklendi}, atlanan: {atlandi} (geçersiz: {gecersizSatir}, kopya: {kopyaSatir}, bekleyen sevkiyat: {bekleyenSevkEngeli})"
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Eksik kayıt eklenemedi: {ex.Message}" });
        }
    }

    private static async Task<BekleyenSevkKontrolSonucu> BekleyenSevkiyatVarMiAsync(MySqlConnection conn, int subeId, int urunId)
    {
        var query = @"
            SELECT u.Ad AS UrunAdi
            FROM stk_Sevkiyat sv
            JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
            JOIN stk_Urun u ON e.UrunId = u.Id
            WHERE e.SubeId = @SubeId
              AND e.UrunId = @UrunId
              AND sv.Durum IN ('Yolda', 'OnayBekliyor')
            LIMIT 1";

        using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@SubeId", subeId);
        cmd.Parameters.AddWithValue("@UrunId", urunId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new BekleyenSevkKontrolSonucu
            {
                VarMi = true,
                UrunAdi = reader["UrunAdi"] == DBNull.Value ? string.Empty : reader["UrunAdi"]?.ToString() ?? string.Empty
            };
        }

        return new BekleyenSevkKontrolSonucu { VarMi = false };
    }

    // GET: Eksik/TopluYukle - Excel yükleme sayfası
    public IActionResult TopluYukle()
    {
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        if (subeId == 0) return RedirectToAction("Login", "Account");
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol == "SubePersoneli")
        {
            TempData["Hata"] = "Excel yükleme yetkiniz bulunmuyor.";
            return RedirectToAction("SubeListe");
        }
        return View("~/Views/STS/Eksik/TopluYukle.cshtml");
    }

    // POST: Eksik/TopluYukle - Excel dosyasını işle
    [HttpPost]
    public async Task<IActionResult> TopluYukle(IFormFile file)
    {
        try
        {
            var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
            var kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
            if (subeId == 0) return RedirectToAction("Login", "Account");
            var rol = HttpContext.Session.GetString("Rol") ?? "";
            if (rol == "SubePersoneli")
            {
                TempData["Hata"] = "Excel yükleme yetkiniz bulunmuyor.";
                return RedirectToAction("SubeListe");
            }

            if (file == null || file.Length == 0)
                return BadRequest("Dosya seçiniz!");

            // Excel import özelliği devre dışı - placeholder
            TempData["Uyari"] = "Excel import özelliği şu anda devre dışıdır. Lütfen eksikleri manuel olarak sisteme ekleyiniz.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Hata: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    // GET: Eksik/TumEksikler - Ana depo tüm eksikleri görüntüleme
    public async Task<IActionResult> TumEksikler()
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var eksikler = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureEksikColumnsAsync(conn);

                var query = @"
                    SELECT e.*, u.Ad as UrunAdi, s.Ad as SubeAdi, s.Kod as SubeKodu,
                           (
                               SELECT COUNT(DISTINCT e2.HaftaNo)
                               FROM stk_EksikKaydi e2
                               WHERE e2.SubeId = e.SubeId
                                 AND e2.UrunId = e.UrunId
                                 AND e2.GirisTarihi >= DATE_SUB(NOW(), INTERVAL 21 DAY)
                           ) as TekrarHaftaSayisi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE e.Durum = 'Bekliyor'
                    ORDER BY e.AcilMi DESC, e.GirisTarihi";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            eksikler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? string.Empty : reader["SiparisNo"]?.ToString() ?? string.Empty,
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                SubeKodu = reader.GetString("SubeKodu"),
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader.GetBoolean("AcilMi"),
                                Not = reader["Aciklama"] == DBNull.Value ? string.Empty : reader["Aciklama"]?.ToString() ?? string.Empty,
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"]),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                TekrarliMi = reader["TekrarHaftaSayisi"] != DBNull.Value && Convert.ToInt32(reader["TekrarHaftaSayisi"]) >= 3
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/Eksik/TumEksikler.cshtml", eksikler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // Helper: Hafta numarası hesapla
    private string GetHaftaNo()
    {
        var today = DateTime.Now;
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var weekOfYear = cultureInfo.Calendar.GetWeekOfYear(today, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
        return $"{today.Year}-W{weekOfYear:D2}";
    }

    private async Task EnsureEksikColumnsAsync(MySqlConnection conn)
    {
        await EnsureColumnAsync(conn, "stk_EksikKaydi", "SiparisNo", "ALTER TABLE stk_EksikKaydi ADD COLUMN SiparisNo VARCHAR(30) NULL AFTER Id;");
        await EnsureColumnAsync(conn, "stk_EksikKaydi", "GeciktiMi", "ALTER TABLE stk_EksikKaydi ADD COLUMN GeciktiMi BOOLEAN NOT NULL DEFAULT FALSE AFTER Aciklama;");

        var indexQuery = @"
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_EksikKaydi'
              AND INDEX_NAME = 'uq_stk_eksikkaydi_siparisno';";

        using (var indexCmd = new MySqlCommand(indexQuery, conn))
        {
            var exists = Convert.ToInt32(await indexCmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var createCmd = new MySqlCommand("CREATE UNIQUE INDEX uq_stk_eksikkaydi_siparisno ON stk_EksikKaydi(SiparisNo);", conn))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }
            }
        }

        using (var fillNoCmd = new MySqlCommand(@"UPDATE stk_EksikKaydi
                SET SiparisNo = CONCAT('SIP-', DATE_FORMAT(GirisTarihi, '%Y%m%d'), '-', LPAD(Id, 4, '0'))
                WHERE SiparisNo IS NULL OR SiparisNo = '';", conn))
        {
            await fillNoCmd.ExecuteNonQueryAsync();
        }

        using (var lateCmd = new MySqlCommand(@"UPDATE stk_EksikKaydi
                SET GeciktiMi = CASE
                    WHEN WEEKDAY(GirisTarihi) > 0 THEN TRUE
                    WHEN WEEKDAY(GirisTarihi) = 0 AND TIME(GirisTarihi) > '17:00:00' THEN TRUE
                    ELSE FALSE
                END;", conn))
        {
            await lateCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task EnsureColumnAsync(MySqlConnection conn, string tableName, string columnName, string alterSql)
    {
        var columnQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @TableName
              AND COLUMN_NAME = @ColumnName;";

        using (var columnCmd = new MySqlCommand(columnQuery, conn))
        {
            columnCmd.Parameters.AddWithValue("@TableName", tableName);
            columnCmd.Parameters.AddWithValue("@ColumnName", columnName);
            var exists = Convert.ToInt32(await columnCmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var alterCmd = new MySqlCommand(alterSql, conn))
                {
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private async Task<string> GenerateEksikSiparisNoAsync(MySqlConnection conn, DateTime now)
    {
        var prefix = $"SIP-{now:yyyyMMdd}-";
        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM stk_EksikKaydi WHERE SiparisNo LIKE @Prefix;", conn))
        {
            cmd.Parameters.AddWithValue("@Prefix", prefix + "%");
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            return prefix + (count + 1).ToString("000");
        }
    }

    private bool IsLateEntry(DateTime dateTime)
    {
        var mondayBasedDay = ((int)dateTime.DayOfWeek + 6) % 7;
        return mondayBasedDay > 0 || (mondayBasedDay == 0 && dateTime.TimeOfDay > new TimeSpan(17, 0, 0));
    }

    private async Task EnsureBildirimTableAsync(MySqlConnection conn)
    {
        var tableQuery = @"
            CREATE TABLE IF NOT EXISTS stk_Bildirim (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                HedefRol VARCHAR(50) NOT NULL,
                HedefSubeId INT NULL,
                Baslik VARCHAR(150) NOT NULL,
                Mesaj VARCHAR(500) NOT NULL,
                Link VARCHAR(250) NULL,
                OkunduMu BOOLEAN NOT NULL DEFAULT FALSE,
                OlusturmaTarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_rol_okundu (HedefRol, OkunduMu),
                INDEX idx_sube (HedefSubeId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;";

        using (var cmd = new MySqlCommand(tableQuery, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateEksikNotificationsAsync(MySqlConnection conn, int subeId, int urunId, string? aciklama, bool geciktiMi)
    {
        await EnsureBildirimTableAsync(conn);

        string subeAdi = string.Empty;
        string urunAdi = string.Empty;
        int? sorumluKullaniciId = null;

        using (var cmd = new MySqlCommand(@"SELECT s.Ad, s.SorumluKullaniciId, u.Ad as UrunAdi
                                           FROM stk_Sube s
                                           JOIN stk_Urun u ON u.Id = @UrunId
                                           WHERE s.Id = @SubeId", conn))
        {
            cmd.Parameters.AddWithValue("@SubeId", subeId);
            cmd.Parameters.AddWithValue("@UrunId", urunId);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    subeAdi = reader.GetString("Ad");
                    urunAdi = reader.GetString("UrunAdi");
                    sorumluKullaniciId = reader["SorumluKullaniciId"] == DBNull.Value ? null : Convert.ToInt32(reader["SorumluKullaniciId"]);
                }
            }
        }

        var mesaj = $"{subeAdi} şubesi {urunAdi} için yeni eksik kaydı oluşturdu.";
        if (!string.IsNullOrWhiteSpace(aciklama))
        {
            mesaj += $" Not: {aciklama}";
        }

        await InsertBildirimAsync(conn, "DepoSorumlusu", null, "Yeni Eksik Kaydı", mesaj, "/Eksik/TumEksikler");
        await InsertBildirimAsync(conn, "Admin", null, "Yeni Eksik Kaydı", mesaj, "/Eksik/TumEksikler");

        if (geciktiMi)
        {
            var gecikmeMesaji = $"{subeAdi} şubesi Pazartesi 17:00 sonrası eksik girişi yaptı.";
            await InsertBildirimAsync(conn, "DepoSorumlusu", null, "Geç Eksik Girişi", gecikmeMesaji, "/RaporSts/Index");
            await InsertBildirimAsync(conn, "Admin", null, "Geç Eksik Girişi", gecikmeMesaji, "/RaporSts/Index");
            if (sorumluKullaniciId.HasValue)
            {
                await InsertBildirimAsync(conn, "SubePersoneli", subeId, "Uyarı", "Eksik girişi son tarih sonrasında yapıldı.", "/Eksik/SubeListe");
            }
        }
    }

    private async Task InsertBildirimAsync(MySqlConnection conn, string hedefRol, int? hedefSubeId, string baslik, string mesaj, string link)
    {
        using (var cmd = new MySqlCommand(@"INSERT INTO stk_Bildirim (HedefRol, HedefSubeId, Baslik, Mesaj, Link, OkunduMu)
                                           VALUES (@HedefRol, @HedefSubeId, @Baslik, @Mesaj, @Link, false)", conn))
        {
            cmd.Parameters.AddWithValue("@HedefRol", hedefRol);
            cmd.Parameters.AddWithValue("@HedefSubeId", hedefSubeId.HasValue ? hedefSubeId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Baslik", baslik);
            cmd.Parameters.AddWithValue("@Mesaj", mesaj);
            cmd.Parameters.AddWithValue("@Link", link);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
}
