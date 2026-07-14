using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using gamabelmvc.Models;
using gamabelmvc.Models.PRS;
using gamabelmvc.Services;

namespace gamabelmvc.Controllers;

public class AccountController : Controller
{
    private readonly string _connectionString;
    private readonly ISystemInfoService _systemInfoService;

    public AccountController(IConfiguration configuration, ISystemInfoService systemInfoService)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
        _systemInfoService = systemInfoService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.KullaniciAdi) || string.IsNullOrWhiteSpace(model.Sifre))
        {
            ViewBag.Hata = "Kullanıcı adı ve şifre boş bırakılamaz.";
            return View(model);
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT id, birim, rol, aktif_mi FROM admin_kullanicilar WHERE kullanici_adi = @kullaniciAdi AND sifre = @sifre",
                connection);
            command.Parameters.AddWithValue("@kullaniciAdi", model.KullaniciAdi);
            command.Parameters.AddWithValue("@sifre", model.Sifre);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var aktifMi = reader.IsDBNull(3) ? false : reader.GetBoolean(3);
                if (!aktifMi)
                {
                    ViewBag.Hata = "Hesabınız henüz aktif edilmemiştir. Lütfen yönetici ile iletişime geçin.";
                    return View(model);
                }

                var personelId = reader.GetInt32(0);
                HttpContext.Session.SetInt32("PersonelId", personelId);
                HttpContext.Session.SetString("KullaniciAdi", model.KullaniciAdi);
                HttpContext.Session.SetString("Birim", reader.IsDBNull(1) ? "" : reader.GetString(1));
                HttpContext.Session.SetString("Rol", reader.IsDBNull(2) ? "birim_amiri" : reader.GetString(2));
                HttpContext.Session.SetString("ActiveModule", "Personel");
                return RedirectToAction("Index", "Kullanici");
            }
            else
            {
                ViewBag.Hata = "Kullanıcı adı veya şifre hatalı.";
                return View(model);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı bağlantı hatası: " + ex.Message;
            return View(model);
        }
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult StsLogin()
    {
        return View(new StsLoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> StsLogin(StsLoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.KullaniciAdi) || string.IsNullOrWhiteSpace(model.Sifre))
        {
            ViewBag.Hata = "Kullanıcı adı ve şifre boş bırakılamaz.";
            return View(model);
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(@"
                SELECT Id, SubeId, KullaniciAdi, SifreHash, Rol, AktifMi
                FROM stk_Kullanici
                WHERE KullaniciAdi = @kullaniciAdi
                LIMIT 1", connection);
            command.Parameters.AddWithValue("@kullaniciAdi", model.KullaniciAdi);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                ViewBag.Hata = "STS kullanıcı adı veya şifre hatalı.";
                return View(model);
            }

            var aktifMi = reader["AktifMi"] != DBNull.Value && Convert.ToBoolean(reader["AktifMi"]);
            if (!aktifMi)
            {
                ViewBag.Hata = "STS hesabı pasif durumda.";
                return View(model);
            }

            var sifreHash = reader["SifreHash"]?.ToString() ?? string.Empty;
            var sifreDogru = false;
            if (!string.IsNullOrWhiteSpace(sifreHash))
            {
                // Geçiş dönemi için hem hash hem düz metin kontrolü
                if (sifreHash.StartsWith("$2"))
                {
                    sifreDogru = BCrypt.Net.BCrypt.Verify(model.Sifre, sifreHash);
                }
                else
                {
                    sifreDogru = string.Equals(model.Sifre, sifreHash, StringComparison.Ordinal);
                }
            }

            if (!sifreDogru)
            {
                ViewBag.Hata = "STS kullanıcı adı veya şifre hatalı.";
                return View(model);
            }

            var kullaniciId = Convert.ToInt32(reader["Id"]);
            
            HttpContext.Session.Clear();
            HttpContext.Session.SetString("ActiveModule", "STS");
            HttpContext.Session.SetInt32("KullaniciId", kullaniciId);
            HttpContext.Session.SetInt32("SubeId", Convert.ToInt32(reader["SubeId"]));
            HttpContext.Session.SetString("KullaniciAdi", reader["KullaniciAdi"]?.ToString() ?? model.KullaniciAdi);
            HttpContext.Session.SetString("Rol", reader["Rol"]?.ToString() ?? "SubePersoneli");

            // Giriş işlemini günlüğe kaydet
            _ = LogUserLogin(kullaniciId, true);

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "STS giriş hatası: " + ex.Message;
            return View(model);
        }
    }

    /// <summary>
    /// Kullanıcı giriş işlemini veritabanına log olarak kaydet
    /// </summary>
    private async Task LogUserLogin(int kullaniciId, bool basariliMi, string hataMesaji = "")
    {
        try
        {
            var ipAdresi = _systemInfoService.GetClientIpAddress(HttpContext);
            var bilgisayarAdi = _systemInfoService.GetComputerName();
            var macAdresi = _systemInfoService.GetMacAddress();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var tarayici = _systemInfoService.GetBrowserInfo(userAgent);
            var isletimSistemi = _systemInfoService.GetOperatingSystem(userAgent);

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO stk_GirisGunlugu 
                (KullaniciId, GirisTarihi, IpAdresi, BilgisayarAdi, MacAdresi, Tarayici, IsletimSistemi, BasariliMi, HataMesaji)
                VALUES 
                (@KullaniciId, NOW(), @IpAdresi, @BilgisayarAdi, @MacAdresi, @Tarayici, @IsletimSistemi, @BasariliMi, @HataMesaji)";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@KullaniciId", kullaniciId);
            command.Parameters.AddWithValue("@IpAdresi", ipAdresi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BilgisayarAdi", bilgisayarAdi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@MacAdresi", macAdresi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Tarayici", tarayici ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsletimSistemi", isletimSistemi ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BasariliMi", basariliMi);
            command.Parameters.AddWithValue("@HataMesaji", hataMesaji ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Log kaydı başarısız olsa bile, uygulamayı etkilemesin
            System.Diagnostics.Debug.WriteLine($"Giriş günlüğü kaydetme hatası: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        await LoadBirimler();
        return View();
    }

    private async Task LoadBirimler()
    {
        var birimler = new List<string>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT birim_adi FROM birimler ORDER BY birim_adi", connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                    birimler.Add(reader.GetString(0));
            }
        }
        catch { }
        ViewBag.Birimler = birimler;
    }

    [HttpPost]
    public async Task<IActionResult> Register(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.KullaniciAdi) || string.IsNullOrWhiteSpace(model.Sifre))
        {
            ViewBag.Hata = "Kullanıcı adı ve şifre boş bırakılamaz.";
            await LoadBirimler();
            return View(model);
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Aynı kullanıcı adı var mı kontrol et
            await using var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM admin_kullanicilar WHERE kullanici_adi = @kullaniciAdi", connection);
            checkCmd.Parameters.AddWithValue("@kullaniciAdi", model.KullaniciAdi);
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (exists > 0)
            {
                ViewBag.Hata = "Bu kullanıcı adı zaten kayıtlı.";
                await LoadBirimler();
                return View(model);
            }

            // Yeni kayıt ekle - aktif_mi = 1 (otomatik aktifleştir)
            await using var insertCmd = new MySqlCommand(
                "INSERT INTO admin_kullanicilar (kullanici_adi, sifre, birim, aktif_mi) VALUES (@kullaniciAdi, @sifre, @birim, 1)", connection);
            insertCmd.Parameters.AddWithValue("@kullaniciAdi", model.KullaniciAdi);
            insertCmd.Parameters.AddWithValue("@sifre", model.Sifre);
            insertCmd.Parameters.AddWithValue("@birim", string.IsNullOrEmpty(model.Birim) ? (object)DBNull.Value : model.Birim);
            await insertCmd.ExecuteNonQueryAsync();

            ViewBag.Basari = "Kayıt başarılı! Giriş yapabilirsiniz.";
            await LoadBirimler();
            return View(new LoginViewModel());
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
            await LoadBirimler();
            return View(model);
        }
    }
}
