using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.STS;

public class RaporController : Controller
{
    private readonly string _connectionString;

    public RaporController(IConfiguration configuration)
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

        ViewBag.Birimler = birimler;
        ViewBag.Rol = rol;
        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetStatuRapor(string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        var sonuclar = new List<object>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql;
            MySqlCommand command;
            if (birim == "__TUMU__" && rol == "admin")
            {
                sql = "SELECT IFNULL(per_statu, 'Belirtilmemiş') AS statu, COUNT(*) AS toplam FROM personeller GROUP BY per_statu ORDER BY toplam DESC";
                command = new MySqlCommand(sql, connection);
            }
            else
            {
                sql = "SELECT IFNULL(per_statu, 'Belirtilmemiş') AS statu, COUNT(*) AS toplam FROM personeller WHERE birim_adi = @birim GROUP BY per_statu ORDER BY toplam DESC";
                command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@birim", birim);
            }
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sonuclar.Add(new
                {
                    statu = reader.GetString(0),
                    toplam = reader.GetInt32(1)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(sonuclar);
    }

    /// <summary>
    /// Seçili birimde o ay izin kullanan personelleri getirir
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIzinRapor(string birim, int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        var sonuclar = new List<object>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql;
            MySqlCommand command;

            if (birim == "__TUMU__" && rol == "admin")
            {
                sql = @"SELECT p.ad, p.soyad, p.birim_adi, pi.izin_tipi, COUNT(*) AS gun_sayisi
                        FROM puantaj_izin pi
                        INNER JOIN personeller p ON p.id = pi.personel_id
                        WHERE pi.yil = @yil AND pi.ay = @ay
                        GROUP BY pi.personel_id, pi.izin_tipi
                        ORDER BY p.birim_adi, p.ad, p.soyad, pi.izin_tipi";
                command = new MySqlCommand(sql, connection);
            }
            else
            {
                sql = @"SELECT p.ad, p.soyad, p.birim_adi, pi.izin_tipi, COUNT(*) AS gun_sayisi
                        FROM puantaj_izin pi
                        INNER JOIN personeller p ON p.id = pi.personel_id
                        WHERE pi.yil = @yil AND pi.ay = @ay AND p.birim_adi = @birim
                        GROUP BY pi.personel_id, pi.izin_tipi
                        ORDER BY p.ad, p.soyad, pi.izin_tipi";
                command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@birim", birim);
            }

            command.Parameters.AddWithValue("@yil", yil);
            command.Parameters.AddWithValue("@ay", ay);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sonuclar.Add(new
                {
                    ad = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    soyad = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    birim = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    izinTipi = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    gunSayisi = reader.GetInt32(4)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(sonuclar);
    }

    /// <summary>
    /// Seçili birimde o ay mesai yapan personelleri getirir
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMesaiRapor(string birim, int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        var sonuclar = new List<object>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string sql;
            MySqlCommand command;

            if (birim == "__TUMU__" && rol == "admin")
            {
                sql = @"SELECT CONCAT(p.ad, ' ', p.soyad) AS ad_soyad, p.birim_adi,
                               COUNT(*) AS mesai_gun,
                               SUM(mk.fiili_saat) AS toplam_fiili,
                               SUM(mk.zam01_saat) AS toplam_zam01,
                               SUM(mk.zam05_saat) AS toplam_zam05,
                               SUM(mk.toplam_saat) AS toplam_saat
                        FROM mesai_kayitlari mk
                        INNER JOIN personeller p ON p.id = mk.personel_id
                        WHERE YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
                        GROUP BY mk.personel_id
                        ORDER BY p.birim_adi, p.ad, p.soyad";
                command = new MySqlCommand(sql, connection);
            }
            else
            {
                sql = @"SELECT CONCAT(p.ad, ' ', p.soyad) AS ad_soyad, p.birim_adi,
                               COUNT(*) AS mesai_gun,
                               SUM(mk.fiili_saat) AS toplam_fiili,
                               SUM(mk.zam01_saat) AS toplam_zam01,
                               SUM(mk.zam05_saat) AS toplam_zam05,
                               SUM(mk.toplam_saat) AS toplam_saat
                        FROM mesai_kayitlari mk
                        INNER JOIN personeller p ON p.id = mk.personel_id
                        WHERE YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay AND p.birim_adi = @birim
                        GROUP BY mk.personel_id
                        ORDER BY p.ad, p.soyad";
                command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@birim", birim);
            }

            command.Parameters.AddWithValue("@yil", yil);
            command.Parameters.AddWithValue("@ay", ay);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sonuclar.Add(new
                {
                    adSoyad = reader.GetString(0),
                    birim = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    mesaiGun = reader.GetInt32(2),
                    toplamFiili = reader.GetDecimal(3),
                    toplamZam01 = reader.GetDecimal(4),
                    toplamZam05 = reader.GetDecimal(5),
                    toplamSaat = reader.GetDecimal(6)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(sonuclar);
    }
}
