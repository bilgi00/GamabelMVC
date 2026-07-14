using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.PRS;

public class MesaiController : Controller
{
    private readonly string _connectionString;

    public MesaiController(IConfiguration configuration)
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

    [HttpGet]
    public async Task<IActionResult> GetPersoneller(string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        // birim != "all" ise, admin olmayan kullanıcılar kendi birimlerine sınırlanır
        if (birim != "all" && rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        var personeller = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = (birim == "all")
                ? "SELECT id, ad, soyad, per_statu, birim_adi FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT id, ad, soyad, per_statu, birim_adi FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";
            await using var cmd = new MySqlCommand(sql, connection);
            if (birim != "all")
                cmd.Parameters.AddWithValue("@birim", birim);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                personeller.Add(new
                {
                    id = reader.GetInt32(0),
                    ad = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    soyad = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    gorev = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    birimAdi = reader.IsDBNull(4) ? "" : reader.GetString(4)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(personeller);
    }

    [HttpGet]
    public async Task<IActionResult> GetMesaiKayitlari(int yil, int ay, string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
            birim = kullaniciBirim;

        var kayitlar = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var birimFiltre = (rol == "admin" && birim == "all")
                ? ""
                : "p.birim_adi = @birim AND ";
            await using var cmd = new MySqlCommand(
                $@"SELECT mk.id, mk.personel_id, CONCAT(p.ad, ' ', p.soyad) AS ad_soyad,
                         mk.tarih, mk.gorev, mk.baslangic, mk.bitis,
                         mk.fiili_saat, mk.zam01_saat, mk.zam05_saat, mk.toplam_saat, mk.aciklama
                  FROM mesai_kayitlari mk
                  INNER JOIN personeller p ON p.id = mk.personel_id
                  WHERE {birimFiltre}YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
                  ORDER BY mk.tarih, p.ad, p.soyad", connection);
            if (!(rol == "admin" && birim == "all"))
                cmd.Parameters.AddWithValue("@birim", birim);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                kayitlar.Add(new
                {
                    id = reader.GetInt32(0),
                    personelId = reader.GetInt32(1),
                    adSoyad = reader.GetString(2),
                    tarih = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                    gorev = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    baslangic = reader.GetTimeSpan(5).ToString(@"hh\:mm"),
                    bitis = reader.GetTimeSpan(6).ToString(@"hh\:mm"),
                    fiiliSaat = reader.GetDecimal(7),
                    zam01Saat = reader.GetDecimal(8),
                    zam05Saat = reader.GetDecimal(9),
                    toplamSaat = reader.GetDecimal(10),
                    aciklama = reader.IsDBNull(11) ? "" : reader.GetString(11)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(kayitlar);
    }

    [HttpPost]
    public async Task<IActionResult> KaydetMesai([FromBody] MesaiKayitDto kayit)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (kayit == null)
            return Json(new { basarili = false, mesaj = "Geçersiz veri." });

        // Ay kilidi kontrolü
        try
        {
            var tarih = DateTime.Parse(kayit.Tarih);
            await using var kilitConn = new MySqlConnection(_connectionString);
            await kilitConn.OpenAsync();
            await using var kilitCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", kilitConn);
            kilitCmd.Parameters.AddWithValue("@yil", tarih.Year);
            kilitCmd.Parameters.AddWithValue("@ay", tarih.Month);
            var kilitSayisi = Convert.ToInt32(await kilitCmd.ExecuteScalarAsync());
            if (kilitSayisi > 0)
                return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Kayıt yapılamaz." });
        }
        catch { }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (kayit.Id > 0)
            {
                // Güncelle
                await using var cmd = new MySqlCommand(
                    @"UPDATE mesai_kayitlari SET personel_id=@pid, tarih=@tarih, gorev=@gorev,
                      baslangic=@bas, bitis=@bit, fiili_saat=@fiili, zam01_saat=@zam01,
                      zam05_saat=@zam05, toplam_saat=@toplam, aciklama=@aciklama WHERE id=@id", connection);
                cmd.Parameters.AddWithValue("@id", kayit.Id);
                cmd.Parameters.AddWithValue("@pid", kayit.PersonelId);
                cmd.Parameters.AddWithValue("@tarih", kayit.Tarih);
                cmd.Parameters.AddWithValue("@gorev", (object?)kayit.Gorev ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@bas", TimeSpan.Parse(kayit.Baslangic));
                cmd.Parameters.AddWithValue("@bit", TimeSpan.Parse(kayit.Bitis));
                cmd.Parameters.AddWithValue("@fiili", kayit.FiiliSaat);
                cmd.Parameters.AddWithValue("@zam01", kayit.Zam01Saat);
                cmd.Parameters.AddWithValue("@zam05", kayit.Zam05Saat);
                cmd.Parameters.AddWithValue("@toplam", kayit.ToplamSaat);
                cmd.Parameters.AddWithValue("@aciklama", (object?)kayit.Aciklama ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                return Json(new { basarili = true, mesaj = "Kayıt güncellendi.", id = kayit.Id });
            }
            else
            {
                // Yeni ekle
                await using var cmd = new MySqlCommand(
                    @"INSERT INTO mesai_kayitlari (personel_id, tarih, gorev, baslangic, bitis, fiili_saat, zam01_saat, zam05_saat, toplam_saat, aciklama)
                      VALUES (@pid, @tarih, @gorev, @bas, @bit, @fiili, @zam01, @zam05, @toplam, @aciklama);
                      SELECT LAST_INSERT_ID();", connection);
                cmd.Parameters.AddWithValue("@pid", kayit.PersonelId);
                cmd.Parameters.AddWithValue("@tarih", kayit.Tarih);
                cmd.Parameters.AddWithValue("@gorev", (object?)kayit.Gorev ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@bas", TimeSpan.Parse(kayit.Baslangic));
                cmd.Parameters.AddWithValue("@bit", TimeSpan.Parse(kayit.Bitis));
                cmd.Parameters.AddWithValue("@fiili", kayit.FiiliSaat);
                cmd.Parameters.AddWithValue("@zam01", kayit.Zam01Saat);
                cmd.Parameters.AddWithValue("@zam05", kayit.Zam05Saat);
                cmd.Parameters.AddWithValue("@toplam", kayit.ToplamSaat);
                cmd.Parameters.AddWithValue("@aciklama", (object?)kayit.Aciklama ?? DBNull.Value);
                var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { basarili = true, mesaj = "Kayıt eklendi.", id = newId });
            }
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SilMesai([FromBody] SilDto dto)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Ay kilidi kontrolü - kaydın tarihini bul
            await using var tarihCmd = new MySqlCommand(
                "SELECT tarih FROM mesai_kayitlari WHERE id = @id", connection);
            tarihCmd.Parameters.AddWithValue("@id", dto.Id);
            var tarihObj = await tarihCmd.ExecuteScalarAsync();
            if (tarihObj != null)
            {
                var tarih = (DateTime)tarihObj;
                await using var kilitCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", connection);
                kilitCmd.Parameters.AddWithValue("@yil", tarih.Year);
                kilitCmd.Parameters.AddWithValue("@ay", tarih.Month);
                var kilitSayisi = Convert.ToInt32(await kilitCmd.ExecuteScalarAsync());
                if (kilitSayisi > 0)
                    return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Silme yapılamaz." });
            }

            await using var cmd = new MySqlCommand("DELETE FROM mesai_kayitlari WHERE id = @id", connection);
            cmd.Parameters.AddWithValue("@id", dto.Id);
            await cmd.ExecuteNonQueryAsync();

            return Json(new { basarili = true, mesaj = "Kayıt silindi." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = "Hata: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSaatlikBrut()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT saatlik_brut FROM mesaisaat ORDER BY id DESC LIMIT 1", connection);
            var result = await cmd.ExecuteScalarAsync();
            var brut = result != null ? Convert.ToDecimal(result) : 0;
            return Json(new { saatlikBrut = brut });
        }
        catch (Exception ex)
        {
            return Json(new { saatlikBrut = 0, hata = ex.Message });
        }
    }

    public async Task<IActionResult> MesaiOdemesi()
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

    [HttpGet]
    public async Task<IActionResult> GetMesaiOdemeleri(int yil, int ay, string birim, bool hesapla = true)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        // Birim seçimi validate
        if (string.IsNullOrEmpty(birim))
        {
            return Json(new
            {
                basarili = false,
                hata = "Birim seçiniz"
            });
        }

        // Admin değilse ve kendi birim olmayan bir birim seçerse izin verme
        if (rol != "admin")
        {
            if (birim == "all")
            {
                return Json(new
                {
                    basarili = false,
                    hata = "Sadece kendi bölümünüzün verilerini görebilirsiniz"
                });
            }
            if (!string.IsNullOrEmpty(kullaniciBirim) && birim != kullaniciBirim)
            {
                birim = kullaniciBirim;
            }
        }

        var odemeler = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Saatlik brüt ücreti çek (sadece hesapla=true ise)
            decimal brut = 0;
            if (hesapla)
            {
                var brutCmd = new MySqlCommand("SELECT saatlik_brut FROM mesaisaat ORDER BY id DESC LIMIT 1", connection);
                var brutResult = await brutCmd.ExecuteScalarAsync();
                brut = brutResult != null ? Convert.ToDecimal(brutResult) : 0;
            }

            var birimFiltre = (rol == "admin" && birim == "all")
                ? ""
                : "p.birim_adi = @birim AND ";

            // Sadece mesaisi olan personelleri getir (INNER JOIN ile)
            await using var cmd = new MySqlCommand(
                $@"SELECT DISTINCT p.id, CONCAT(p.ad, ' ', p.soyad) AS ad_soyad, p.birim_adi,
                         COALESCE(SUM(mk.toplam_saat), 0) AS toplam_saat
                  FROM personeller p
                  INNER JOIN mesai_kayitlari mk ON mk.personel_id = p.id 
                    AND YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
                  WHERE {birimFiltre}p.per_statu IS NOT NULL
                  GROUP BY p.id, p.ad, p.soyad, p.birim_adi
                  ORDER BY p.ad, p.soyad", connection);

            if (!(rol == "admin" && birim == "all"))
                cmd.Parameters.AddWithValue("@birim", birim);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            var sira = 0;
            decimal toplamMesai = 0, toplamOdeme = 0;
            
            while (await reader.ReadAsync())
            {
                sira++;
                var personelId = reader.GetInt32(0);
                var adSoyad = reader.GetString(1);
                var birimAdi = reader.GetString(2);
                var toplamSaat = reader.GetDecimal(3);
                
                // Hesapla parametresine göre ödeme hesapla veya 0 göster
                var odeme = hesapla ? (toplamSaat * brut) : 0;

                odemeler.Add(new
                {
                    sira,
                    personelId,
                    adSoyad,
                    birimAdi,
                    toplamSaat = Math.Round(toplamSaat, 2),
                    saatlikBrut = Math.Round(brut, 2),
                    odemeTutari = Math.Round(odeme, 2),
                    saatlikBrutFormatted = brut.ToString("0.00"),
                    odemeTutariFormatted = odeme.ToString("0.00")
                });

                toplamMesai += toplamSaat;
                toplamOdeme += odeme;
            }

            return Json(new
            {
                basarili = true,
                odemeler,
                ozet = new
                {
                    toplamPersonel = sira,
                    toplamMesai = Math.Round(toplamMesai, 2),
                    saatlikBrut = Math.Round(brut, 2),
                    genelToplamOdeme = Math.Round(toplamOdeme, 2),
                    toplamMesaiFormatted = toplamMesai.ToString("0.00"),
                    saatlikBrutFormatted = brut.ToString("0.00"),
                    genelToplamOdemeFormatted = toplamOdeme.ToString("0.00")
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, hata = ex.Message });
        }
    }

    public class MesaiKayitDto
    {
        public int Id { get; set; }
        public int PersonelId { get; set; }
        public string Tarih { get; set; } = "";
        public string? Gorev { get; set; }
        public string Baslangic { get; set; } = "08:00";
        public string Bitis { get; set; } = "17:00";
        public decimal FiiliSaat { get; set; }
        public decimal Zam01Saat { get; set; }
        public decimal Zam05Saat { get; set; }
        public decimal ToplamSaat { get; set; }
        public string? Aciklama { get; set; }
    }

    public class SilDto
    {
        public int Id { get; set; }
    }
}
