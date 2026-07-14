using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Controllers.PRS;

public class PuantajController : Controller
{
    private readonly string _connectionString;

    public PuantajController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        var birimler = new List<string>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (rol == "admin")
            {
                await using var cmd = new MySqlCommand("SELECT birim_adi FROM birimler ORDER BY birim_adi", connection);
                await using var reader = await cmd.ExecuteReaderAsync();
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

        ViewBag.Birimler = birimler;
        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        ViewBag.Rol = rol;
        ViewBag.KullaniciBirim = kullaniciBirim;
        return View();
    }

    // Birime göre personel listesi
    [HttpGet]
    public async Task<IActionResult> GetPersoneller(string birim)
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

        var personeller = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = (rol == "admin" && birim == "all")
                ? "SELECT id, ad, soyad, birim_adi FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT id, ad, soyad, birim_adi FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";

            await using var cmd = new MySqlCommand(sql, connection);
            if (!(rol == "admin" && birim == "all"))
                cmd.Parameters.AddWithValue("@birim", birim);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                personeller.Add(new
                {
                    id = reader.GetInt32(0),
                    ad = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    soyad = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    birim = reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(personeller);
    }

    // Belirli ay/yıl için izin kayıtlarını getir
    [HttpGet]
    public async Task<IActionResult> GetIzinler(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var izinler = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT id, personel_id, gun, izin_tipi, aciklama FROM puantaj_izin WHERE yil = @yil AND ay = @ay",
                connection);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                izinler.Add(new
                {
                    id = reader.GetInt32(0),
                    personelId = reader.GetInt32(1),
                    gun = reader.GetInt32(2),
                    izinTipi = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    aciklama = reader.IsDBNull(4) ? "" : reader.GetString(4)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(izinler);
    }

    // İzin kaydet (tek veya toplu)
    [HttpPost]
    public async Task<IActionResult> KaydetIzin([FromBody] List<PuantajIzinModel> izinler)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (izinler == null || izinler.Count == 0)
            return Json(new { basarili = false, mesaj = "Kaydedilecek veri yok" });

        // Ay kilidi kontrolü
        var ilkIzin = izinler.First();
        try
        {
            await using var kilitConn = new MySqlConnection(_connectionString);
            await kilitConn.OpenAsync();
            await using var kilitCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", kilitConn);
            kilitCmd.Parameters.AddWithValue("@yil", ilkIzin.Yil);
            kilitCmd.Parameters.AddWithValue("@ay", ilkIzin.Ay);
            var kilitSayisi = Convert.ToInt32(await kilitCmd.ExecuteScalarAsync());
            if (kilitSayisi > 0)
                return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Kayıt yapılamaz." });
        }
        catch { }

        int eklenen = 0, silinen = 0;
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var izin in izinler)
            {
                // İzin tipi X ise (çalıştı) kaydı sil — sadece izin günleri DB'de tutulur
                if (izin.IzinTipi == "X" || string.IsNullOrEmpty(izin.IzinTipi))
                {
                    await using var delCmd = new MySqlCommand(
                        "DELETE FROM puantaj_izin WHERE personel_id = @pid AND yil = @yil AND ay = @ay AND gun = @gun",
                        connection);
                    delCmd.Parameters.AddWithValue("@pid", izin.PersonelId);
                    delCmd.Parameters.AddWithValue("@yil", izin.Yil);
                    delCmd.Parameters.AddWithValue("@ay", izin.Ay);
                    delCmd.Parameters.AddWithValue("@gun", izin.Gun);
                    var deleted = await delCmd.ExecuteNonQueryAsync();
                    if (deleted > 0) silinen++;
                }
                else
                {
                    // UPSERT: varsa güncelle, yoksa ekle
                    await using var upsertCmd = new MySqlCommand(
                        @"INSERT INTO puantaj_izin (personel_id, yil, ay, gun, izin_tipi, aciklama)
                          VALUES (@pid, @yil, @ay, @gun, @tip, @aciklama)
                          ON DUPLICATE KEY UPDATE izin_tipi = @tip, aciklama = @aciklama",
                        connection);
                    upsertCmd.Parameters.AddWithValue("@pid", izin.PersonelId);
                    upsertCmd.Parameters.AddWithValue("@yil", izin.Yil);
                    upsertCmd.Parameters.AddWithValue("@ay", izin.Ay);
                    upsertCmd.Parameters.AddWithValue("@gun", izin.Gun);
                    upsertCmd.Parameters.AddWithValue("@tip", izin.IzinTipi);
                    upsertCmd.Parameters.AddWithValue("@aciklama", izin.Aciklama ?? "");
                    await upsertCmd.ExecuteNonQueryAsync();
                    eklenen++;
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = ex.Message });
        }

        return Json(new { basarili = true, mesaj = $"{eklenen} izin kaydedildi, {silinen} kayıt silindi" });
    }

    // Resmi tatilleri getir (yıl ve ay bazlı)
    [HttpGet]
    public async Task<IActionResult> GetResmiTatiller(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var tatiller = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT DAY(tatil_tarihi) AS gun, tatil_adi FROM kktc_resmi_tatiller WHERE YEAR(tatil_tarihi) = @yil AND MONTH(tatil_tarihi) = @ay ORDER BY tatil_tarihi",
                connection);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tatiller.Add(new
                {
                    gun = reader.GetInt32(0),
                    tatilAdi = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(tatiller);
    }

    // Excel/PDF çıktı sayfası
    public IActionResult Cikti(int? yil, int? ay, string? birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        // Admin değilse birim kısıtla
        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        ViewBag.Yil = yil ?? DateTime.Now.Year;
        ViewBag.Ay = ay ?? DateTime.Now.Month;
        ViewBag.Birim = birim ?? (rol == "admin" ? "all" : kullaniciBirim);
        ViewBag.Rol = rol;
        ViewBag.KullaniciBirim = kullaniciBirim;
        return View();
    }
}
