using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Controllers.PRS;

public class KullaniciController : Controller
{
    private readonly string _connectionString;

    public KullaniciController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    public async Task<IActionResult> Index()
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        // Sadece admin görebilir
        if (HttpContext.Session.GetString("Rol") != "admin")
        {
            return RedirectToAction("Liste2");
        }

        var kullanicilar = new List<KullaniciModel>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("SELECT * FROM admin_kullanicilar", connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var kullanici = new KullaniciModel();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i).ToLower();
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();

                    switch (columnName)
                    {
                        case "id":
                            kullanici.Id = reader.GetInt32(i);
                            break;
                        case "kullanici_adi":
                            kullanici.KullaniciAdi = value;
                            break;
                        case "ad":
                            kullanici.Ad = value;
                            break;
                        case "soyad":
                            kullanici.Soyad = value;
                            break;
                        case "telefon":
                            kullanici.Telefon = value;
                            break;
                        case "birim":
                            kullanici.Birim = value;
                            break;
                        case "rol":
                            kullanici.Rol = value;
                            break;
                    }
                }

                kullanicilar.Add(kullanici);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        return View(kullanicilar);
    }

    public async Task<IActionResult> Liste2()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        var birimler = new List<string>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (rol == "admin")
            {
                await using var command = new MySqlCommand("SELECT birim_adi FROM birimler ORDER BY birim_adi", connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        birimler.Add(reader.GetString(0));
                }
            }
            else if (!string.IsNullOrEmpty(kullaniciBirim))
            {
                birimler.Add(kullaniciBirim);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        ViewBag.Birimler = birimler;
        ViewBag.Rol = rol;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PersonelByBirim(string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        // Admin değilse, sadece kendi birimini görebilir
        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
        {
            birim = kullaniciBirim;
        }

        var personeller = new List<PersonelModel>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT ad, soyad, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad", connection);
            command.Parameters.AddWithValue("@birim", birim);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                personeller.Add(new PersonelModel
                {
                    Ad = reader.IsDBNull(0) ? null : reader.GetString(0),
                    Soyad = reader.IsDBNull(1) ? null : reader.GetString(1),
                    PerStatu = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(personeller);
    }

    [HttpPost]
    public async Task<IActionResult> ExcelAktar(IFormFile excelFile)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (HttpContext.Session.GetString("Rol") != "admin")
            return Json(new { basarili = false, mesaj = "Bu işlem için admin yetkisi gereklidir." });

        if (excelFile == null || excelFile.Length == 0)
            return Json(new { basarili = false, mesaj = "Lütfen bir Excel dosyası seçin." });

        var ext = Path.GetExtension(excelFile.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return Json(new { basarili = false, mesaj = "Sadece .xlsx veya .xls dosyaları kabul edilir." });

        // Excel import özelliği devre dışı - placeholder
        return Json(new { basarili = false, mesaj = "Excel import özelliği şu anda devre dışıdır. Lütfen personel bilgilerini manuel olarak sisteme ekleyiniz." });
    }

    /// <summary>
    /// Kullanıcı profil sayfasını gösterir
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Profil()
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        var kullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        var kullanici = new KullaniciModel();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Email sütununu ekle (gerekirse)
            await EnsureAdminEmailColumnAsync(connection);

            await using var command = new MySqlCommand(
                "SELECT id, ad, soyad, telefon, email, birim, rol FROM admin_kullanicilar WHERE kullanici_adi = @kullaniciAdi",
                connection);
            command.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                kullanici.Id = reader.GetInt32(0);
                kullanici.KullaniciAdi = kullaniciAdi;
                kullanici.Ad = reader.IsDBNull(1) ? null : reader.GetString(1);
                kullanici.Soyad = reader.IsDBNull(2) ? null : reader.GetString(2);
                kullanici.Telefon = reader.IsDBNull(3) ? null : reader.GetString(3);
                kullanici.Email = reader.IsDBNull(4) ? null : reader.GetString(4);
                kullanici.Birim = reader.IsDBNull(5) ? null : reader.GetString(5);
                kullanici.Rol = reader.IsDBNull(6) ? "birim_amiri" : reader.GetString(6);
            }
            else
            {
                ViewBag.Hata = "Kullanıcı bilgileri bulunamadı.";
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.KullaniciAdi = kullaniciAdi;
        return View(kullanici);
    }

    /// <summary>
    /// Kullanıcı şifresini değiştirir
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SifreDegistir(string eskiSifre, string yeniSifre, string yeniSifreOnayla)
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        var kullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");

        // Validasyon
        if (string.IsNullOrWhiteSpace(eskiSifre))
        {
            ViewBag.Hata = "Eski şifre boş bırakılamaz.";
            return await Profil();
        }

        if (string.IsNullOrWhiteSpace(yeniSifre))
        {
            ViewBag.Hata = "Yeni şifre boş bırakılamaz.";
            return await Profil();
        }

        if (yeniSifre != yeniSifreOnayla)
        {
            ViewBag.Hata = "Yeni şifreler eşleşmiyor.";
            return await Profil();
        }

        if (yeniSifre.Length < 3)
        {
            ViewBag.Hata = "Şifre en az 3 karakter olmalıdır.";
            return await Profil();
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Eski şifreyi kontrol et
            await using var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM admin_kullanicilar WHERE kullanici_adi = @kullaniciAdi AND sifre = @eskiSifre",
                connection);
            checkCmd.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);
            checkCmd.Parameters.AddWithValue("@eskiSifre", eskiSifre);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                ViewBag.Hata = "Eski şifre hatalı.";
                return await Profil();
            }

            // Yeni şifreyi kaydet
            await using var updateCmd = new MySqlCommand(
                "UPDATE admin_kullanicilar SET sifre = @yeniSifre WHERE kullanici_adi = @kullaniciAdi",
                connection);
            updateCmd.Parameters.AddWithValue("@yeniSifre", yeniSifre);
            updateCmd.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);
            await updateCmd.ExecuteNonQueryAsync();

            ViewBag.Basari = "Şifreniz başarıyla değiştirildi.";
            return await Profil();
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
            return await Profil();
        }
    }

    /// <summary>
    /// Admin kullanıcısının email adresini günceller
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EmailGuncelle(string email)
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        // Sadece admin için
        if (HttpContext.Session.GetString("Rol") != "admin")
        {
            ViewBag.Hata = "Bu işlem için admin yetkisi gereklidir.";
            return await Profil();
        }

        // Email doğrulaması
        if (string.IsNullOrWhiteSpace(email))
        {
            ViewBag.Hata = "Email adresi boş bırakılamaz.";
            return await Profil();
        }

        // Basit email format kontrolü
        if (!email.Contains("@") || !email.Contains("."))
        {
            ViewBag.Hata = "Geçerli bir email adresi girin.";
            return await Profil();
        }

        var kullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Email adresini güncelle
            await using var updateCmd = new MySqlCommand(
                "UPDATE admin_kullanicilar SET email = @email WHERE kullanici_adi = @kullaniciAdi",
                connection);
            updateCmd.Parameters.AddWithValue("@email", email.Trim());
            updateCmd.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);
            await updateCmd.ExecuteNonQueryAsync();

            ViewBag.Basari = $"Email adresiniz başarıyla güncellendi: {email}";
            return await Profil();
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
            return await Profil();
        }
    }

    /// <summary>
    /// Admin yetkilendirme sayfasını gösterir - tüm kullanıcıları listeler
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Yetkilendirme()
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return RedirectToAction("Login", "Account");
        }

        // Sadece admin görebilir
        if (HttpContext.Session.GetString("Rol") != "admin")
        {
            return RedirectToAction("Liste2");
        }

        var kullanicilar = new List<KullaniciModel>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("SELECT id, kullanici_adi, ad, soyad, birim, rol, aktif_mi FROM admin_kullanicilar ORDER BY kullanici_adi", connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var kullanici = new KullaniciModel
                {
                    Id = reader.GetInt32(0),
                    KullaniciAdi = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Ad = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Soyad = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Birim = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Rol = reader.IsDBNull(5) ? "birim_amiri" : reader.GetString(5),
                    AktifMi = reader.IsDBNull(6) ? false : reader.GetBoolean(6)
                };
                kullanicilar.Add(kullanici);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");

        // Kilitli ayları getir
        var kilitliAylar = new List<object>();
        try
        {
            await using var conn2 = new MySqlConnection(_connectionString);
            await conn2.OpenAsync();
            await using var cmd2 = new MySqlCommand("SELECT yil, ay FROM ay_kilitleri ORDER BY yil, ay", conn2);
            await using var rdr2 = await cmd2.ExecuteReaderAsync();
            while (await rdr2.ReadAsync())
            {
                kilitliAylar.Add(new { yil = rdr2.GetInt32(0), ay = rdr2.GetInt32(1) });
            }
        }
        catch { }
        ViewBag.KilitliAylar = System.Text.Json.JsonSerializer.Serialize(kilitliAylar);

        return View(kullanicilar);
    }

    public class RolDegistirRequest
    {
        public int Id { get; set; }
        public string YeniRol { get; set; } = "";
    }

    /// <summary>
    /// Kullanıcı rolünü değiştirir (admin: POST işlemi)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RolDegistir([FromBody] RolDegistirRequest request)
    {
        // Oturum kontrolü
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
        {
            return Json(new { basarili = false, mesaj = "Oturum geçersiz." });
        }

        // Sadece admin yapabilir
        if (HttpContext.Session.GetString("Rol") != "admin")
        {
            return Json(new { basarili = false, mesaj = "Bu işlem için admin yetkisi gereklidir." });
        }

        var id = request.Id;
        var yeniRol = request.YeniRol;

        // Rol doğrulaması
        if (yeniRol != "admin" && yeniRol != "birim_amiri")
        {
            return Json(new { basarili = false, mesaj = "Geçersiz rol." });
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "UPDATE admin_kullanicilar SET rol = @rol WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@rol", yeniRol);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();

            return Json(new { basarili = true, mesaj = "Rol başarıyla değiştirildi." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    /// <summary>
    /// Kullanıcı aktif/pasif durumunu değiştirir
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AktifDegistir([FromBody] AktifDegistirRequest request)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Json(new { basarili = false, mesaj = "Oturum geçersiz." });

        if (HttpContext.Session.GetString("Rol") != "admin")
            return Json(new { basarili = false, mesaj = "Bu işlem için admin yetkisi gereklidir." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "UPDATE admin_kullanicilar SET aktif_mi = @aktif WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@aktif", request.Aktif);
            command.Parameters.AddWithValue("@id", request.Id);
            await command.ExecuteNonQueryAsync();

            var durum = request.Aktif ? "aktif" : "pasif";
            return Json(new { basarili = true, mesaj = $"Kullanıcı {durum} yapıldı." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    public class AktifDegistirRequest
    {
        public int Id { get; set; }
        public bool Aktif { get; set; }
    }

    /// <summary>
    /// Kullanıcıyı siler
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> KullaniciSil([FromBody] KullaniciSilRequest request)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Json(new { basarili = false, mesaj = "Oturum geçersiz." });

        if (HttpContext.Session.GetString("Rol") != "admin")
            return Json(new { basarili = false, mesaj = "Bu işlem için admin yetkisi gereklidir." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "DELETE FROM admin_kullanicilar WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@id", request.Id);
            var affected = await command.ExecuteNonQueryAsync();

            if (affected > 0)
                return Json(new { basarili = true, mesaj = "Kullanıcı silindi." });
            else
                return Json(new { basarili = false, mesaj = "Kullanıcı bulunamadı." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    public class KullaniciSilRequest
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// Ay kilidini aç/kapa
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AyKilidiDegistir([FromBody] AyKilidiRequest request)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Json(new { basarili = false, mesaj = "Oturum geçersiz." });

        if (HttpContext.Session.GetString("Rol") != "admin")
            return Json(new { basarili = false, mesaj = "Bu işlem için admin yetkisi gereklidir." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (request.Kilitli)
            {
                await using var cmd = new MySqlCommand(
                    "INSERT IGNORE INTO ay_kilitleri (yil, ay) VALUES (@yil, @ay)", connection);
                cmd.Parameters.AddWithValue("@yil", request.Yil);
                cmd.Parameters.AddWithValue("@ay", request.Ay);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmd = new MySqlCommand(
                    "DELETE FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", connection);
                cmd.Parameters.AddWithValue("@yil", request.Yil);
                cmd.Parameters.AddWithValue("@ay", request.Ay);
                await cmd.ExecuteNonQueryAsync();
            }

            var ayAdlari = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
            var durum = request.Kilitli ? "kilitlendi" : "kilidi açıldı";
            return Json(new { basarili = true, mesaj = $"{ayAdlari[request.Ay]} {request.Yil} {durum}." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    /// <summary>
    /// Belirli ay/yıl için kilit durumunu kontrol eder (Puantaj/Mesai tarafından kullanılır)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AyKilitliMi(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", connection);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Json(new { kilitli = count > 0 });
        }
        catch
        {
            return Json(new { kilitli = false });
        }
    }

    /// <summary>
    /// Admin kullanıcılar tablosuna email sütunu ekle (gerekirse)
    /// </summary>
    private async Task EnsureAdminEmailColumnAsync(MySqlConnection conn)
    {
        try
        {
            var checkQuery = @"
                SELECT COUNT(*) FROM information_schema.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                AND TABLE_NAME = 'admin_kullanicilar' 
                AND COLUMN_NAME = 'email'";

            using (var cmd = new MySqlCommand(checkQuery, conn))
            {
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                if (!exists)
                {
                    using (var alterCmd = new MySqlCommand(
                        "ALTER TABLE admin_kullanicilar ADD COLUMN email VARCHAR(255) NULL COMMENT 'Admin kullanıcısının email adresi (raporlar için)' AFTER sifre",
                        conn))
                    {
                        await alterCmd.ExecuteNonQueryAsync();
                    }

                    // Email sütununa index ekle
                    try
                    {
                        using (var indexCmd = new MySqlCommand(
                            "ALTER TABLE admin_kullanicilar ADD INDEX idx_email (email)",
                            conn))
                        {
                            await indexCmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public class AyKilidiRequest
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public bool Kilitli { get; set; }
    }
}
