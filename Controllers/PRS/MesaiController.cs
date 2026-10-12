using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using System.Data;
using gamabelmvc.Services;
using gamabelmvc.Services.Raporlama;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMenuMesai")]
public class MesaiController : Controller
{
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;
    private readonly IRaporServisi _raporServisi;

    private static readonly RaporTanimi MesaiRaporTanimi = RaporKaydi.Mesai;

    public MesaiController(IConfiguration configuration, IWebHostEnvironment environment, IRaporServisi raporServisi)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
        _environment = environment;
        _raporServisi = raporServisi;
    }

    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
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
    public async Task<IActionResult> AyKilitliMi(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();
        if (yil < 1 || ay is < 1 or > 12)
            return BadRequest(new { mesaj = "Geçersiz yıl veya ay." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return Json(new { kilitli = await AyKilitKontrol.KilitliMiAsync(connection, yil, ay) });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mesaj = "Ay kilit durumu kontrol edilemedi.", hata = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPersoneller(string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
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
                ? "SELECT per_no, ad, soyad, per_statu, birim_adi FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT per_no, ad, soyad, per_statu, birim_adi FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";
            await using var cmd = new MySqlCommand(sql, connection);
            if (birim != "all")
                cmd.Parameters.AddWithValue("@birim", birim);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                personeller.Add(new
                {
                    perNo = reader.IsDBNull(0) ? "" : reader.GetString(0),
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

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
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
                $@"SELECT mk.id, mk.personel_id, p.per_no, CONCAT(p.ad, ' ', p.soyad) AS ad_soyad,
                            mk.tarih, mk.gorev, mk.baslangic, mk.bitis,
                         mk.fiili_saat, mk.zam01_saat, mk.zam05_saat, mk.toplam_saat, mk.aciklama
                  FROM mesai_kayitlari mk
                        INNER JOIN personeller p ON p.per_no = mk.personel_id
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
                    personelId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    perNo = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    adSoyad = reader.GetString(3),
                    tarih = reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                    gorev = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    baslangic = reader.GetTimeSpan(6).ToString(@"hh\:mm"),
                    bitis = reader.GetTimeSpan(7).ToString(@"hh\:mm"),
                    fiiliSaat = reader.GetDecimal(8),
                    zam01Saat = reader.GetDecimal(9),
                    zam05Saat = reader.GetDecimal(10),
                    toplamSaat = reader.GetDecimal(11),
                    aciklama = reader.IsDBNull(12) ? "" : reader.GetString(12)
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

        if (string.IsNullOrWhiteSpace(kayit.PerNo))
            return Json(new { basarili = false, mesaj = "Personel per_no bilgisi zorunludur." });

        if (!DateTime.TryParse(kayit.Tarih, out var tarih))
            return Json(new { basarili = false, mesaj = "Geçersiz mesai tarihi." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (kayit.Id > 0)
            {
                await using var mevcutTarihCmd = new MySqlCommand(
                    "SELECT tarih FROM mesai_kayitlari WHERE id = @id", connection);
                mevcutTarihCmd.Parameters.AddWithValue("@id", kayit.Id);
                var mevcutTarih = await mevcutTarihCmd.ExecuteScalarAsync();
                if (mevcutTarih == null || mevcutTarih == DBNull.Value)
                    return Json(new { basarili = false, mesaj = "Mesai kaydı bulunamadı." });

                var oncekiTarih = Convert.ToDateTime(mevcutTarih);
                if (await AyKilitKontrol.KilitliMiAsync(connection, oncekiTarih.Year, oncekiTarih.Month))
                    return Json(new { basarili = false, mesaj = "Kaydın bulunduğu ay kilitlenmiştir. Değişiklik yapılamaz." });
            }

            if (await AyKilitKontrol.KilitliMiAsync(connection, tarih.Year, tarih.Month))
                return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Kayıt veya değişiklik yapılamaz." });

            await using var personelCmd = new MySqlCommand(
                "SELECT per_no FROM personeller WHERE per_no = @perNo LIMIT 1", connection);
            personelCmd.Parameters.AddWithValue("@perNo", kayit.PerNo.Trim());
            var personelIdValue = await personelCmd.ExecuteScalarAsync();
            if (personelIdValue == null || personelIdValue == DBNull.Value)
                return Json(new { basarili = false, mesaj = "Personel per_no ile bulunamadı." });
            var personelNo = Convert.ToString(personelIdValue) ?? "";

            if (kayit.Id > 0)
            {
                // Güncelle
                await using var cmd = new MySqlCommand(
                    @"UPDATE mesai_kayitlari SET personel_id=@pid, tarih=@tarih, gorev=@gorev,
                      baslangic=@bas, bitis=@bit, fiili_saat=@fiili, zam01_saat=@zam01,
                      zam05_saat=@zam05, toplam_saat=@toplam, aciklama=@aciklama WHERE id=@id", connection);
                cmd.Parameters.AddWithValue("@id", kayit.Id);
                cmd.Parameters.AddWithValue("@pid", personelNo);
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
                cmd.Parameters.AddWithValue("@pid", personelNo);
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

        if (dto == null || dto.Id <= 0)
            return Json(new { basarili = false, mesaj = "Geçersiz kayıt bilgisi." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var tarihCmd = new MySqlCommand(
                "SELECT tarih FROM mesai_kayitlari WHERE id = @id", connection);
            tarihCmd.Parameters.AddWithValue("@id", dto.Id);
            var tarihObj = await tarihCmd.ExecuteScalarAsync();
            if (tarihObj == null || tarihObj == DBNull.Value)
                return NotFound(new { basarili = false, mesaj = "Mesai kaydı bulunamadı." });

            var tarih = Convert.ToDateTime(tarihObj);
            if (await AyKilitKontrol.KilitliMiAsync(connection, tarih.Year, tarih.Month))
                return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Silme yapılamaz." });

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

    // ---- FastReport A4 Rapor Standardı (v3) ----
    // Önizleme ve PDF aynı .frx şablonundan üretilir: Reports/Mesai/Mesai.Yatay.frx
    // Excel çıktısı kaldırıldı (standart Bölüm 12: Excel .frx'ten üretilmez).

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Rapor(int yil, int ay, string birim, string? personel = null, string yon = "yatay")
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");
        if (!MesaiRaporTanimi.YonDestekliyor(y))
            return BadRequest("Mesai raporu için yalnızca 'yatay' şablonu mevcuttur.");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (ay is < 1 or > 12 || string.IsNullOrWhiteSpace(birim))
            return BadRequest("Geçerli yıl, ay ve birim bilgisi gereklidir.");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
        {
            if (birim == "all")
                return Forbid();

            if (!string.IsNullOrWhiteSpace(kullaniciBirim))
                birim = kullaniciBirim;
        }

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin", personel);
        var (ds, prm) = MesaiRaporVerisiHazirla(mesailer, yil, ay, birim, personel);

        var web = _raporServisi.Onizleme(MesaiRaporTanimi, y, ds, prm);
        ViewBag.Yon = y;
        ViewBag.DesteklenenYonler = MesaiRaporTanimi.Yonler();
        ViewBag.RaporQuery = $"yil={yil}&ay={ay}&birim={Uri.EscapeDataString(birim)}" +
            (string.IsNullOrWhiteSpace(personel) ? "" : $"&personel={Uri.EscapeDataString(personel)}");
        ViewBag.PdfUrl = Url.Action(nameof(RaporPdf), new { yil, ay, birim, personel, yon = y.ToString().ToLowerInvariant(), indir = true });
        return View("~/Views/Shared/Rapor.cshtml", web);
    }

    // PDF İNDİR / TARAYICIDA AÇ — aynı .frx şablonundan üretilir (A4 yatay).
    [HttpGet]
    public async Task<IActionResult> RaporPdf(int yil, int ay, string birim, string? personel = null, string yon = "yatay", bool indir = false)
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");
        if (!MesaiRaporTanimi.YonDestekliyor(y))
            return BadRequest("Mesai raporu için yalnızca 'yatay' şablonu mevcuttur.");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (ay is < 1 or > 12 || string.IsNullOrWhiteSpace(birim))
            return BadRequest("Geçerli yıl, ay ve birim bilgisi gereklidir.");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
        {
            if (birim == "all")
                return Forbid();

            if (!string.IsNullOrWhiteSpace(kullaniciBirim))
                birim = kullaniciBirim;
        }

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin", personel);
        var (ds, prm) = MesaiRaporVerisiHazirla(mesailer, yil, ay, birim, personel);
        var cikti = _raporServisi.Pdf(MesaiRaporTanimi, y, ds, prm);

        // Hassas rapor yanıtı istemci/proxy önbelleğine alınmaz.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var personelEk = string.IsNullOrWhiteSpace(personel) ? "" : $"-{Uri.EscapeDataString(personel)}";
        var dosyaAdi = $"mesai-{yil:D4}-{ay:D2}{personelEk}.pdf";
        return indir ? File(cikti.Icerik, "application/pdf", dosyaAdi)
                     : File(cikti.Icerik, "application/pdf");
    }

    // HTML DIŞA AKTAR — .frx şablonundan üretilir (RaporCiktiTuru.Html).
    // Excel'den farklı olarak tasarım şablondan gelir; .frx değişince HTML de değişir.
    [HttpGet]
    public async Task<IActionResult> RaporHtml(int yil, int ay, string birim, string? personel = null, string yon = "yatay", bool indir = false)
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");
        if (!MesaiRaporTanimi.YonDestekliyor(y))
            return BadRequest("Mesai raporu için yalnızca 'yatay' şablonu mevcuttur.");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();
        if (ay is < 1 or > 12 || string.IsNullOrWhiteSpace(birim))
            return BadRequest("Geçerli yıl, ay ve birim bilgisi gereklidir.");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
        {
            if (birim == "all")
                return Forbid();

            if (!string.IsNullOrWhiteSpace(kullaniciBirim))
                birim = kullaniciBirim;
        }

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin", personel);
        var (ds, prm) = MesaiRaporVerisiHazirla(mesailer, yil, ay, birim, personel);
        var cikti = _raporServisi.Cikar(MesaiRaporTanimi, RaporCiktiTuru.Html, y, ds, prm);

        // Hassas rapor yanıtı istemci/proxy önbelleğine alınmaz.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        return indir ? File(cikti.Icerik, "text/html; charset=utf-8", $"mesai-{yil:D4}-{ay:D2}.html")
                     : File(cikti.Icerik, "text/html; charset=utf-8");
    }

    // EXCEL DIŞA AKTAR — Mesai verisi .xlsx olarak indirilir (EPPlus, NonCommercial lisans).
    // Yetki/kapsam kuralları PDF ile aynıdır: admin olmayan kendi birimine kilitlidir.
    [HttpGet]
    public async Task<IActionResult> RaporExcel(int yil, int ay, string birim, string? personel = null)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();
        if (ay is < 1 or > 12 || string.IsNullOrWhiteSpace(birim))
            return BadRequest("Geçerli yıl, ay ve birim bilgisi gereklidir.");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
        {
            if (birim == "all")
                return Forbid();

            if (!string.IsNullOrWhiteSpace(kullaniciBirim))
                birim = kullaniciBirim;
        }

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin", personel);

        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new OfficeOpenXml.ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Ek Mesai Çizelgesi");

        const int kolonSayisi = 10;

        // Personel filtresi varsa başlık etiketine personel adı eklenir (PDF ile aynı gösterim).
        var personelNo = (personel ?? "").Trim();
        var kapsamEtiketi = birim == "all" ? "Tüm Personeller" : birim;
        if (personelNo.Length > 0)
        {
            var adSoyad = mesailer.Rows.Count > 0 ? Convert.ToString(mesailer.Rows[0]["AdSoyad"]) : personelNo;
            kapsamEtiketi = $"{kapsamEtiketi} · {adSoyad}";
        }

        // Başlık bloğu (satır 1-3)
        ws.Cells[1, 1, 1, kolonSayisi].Merge = true;
        ws.Cells[1, 1].Value = "GAMABEL YATIRIM LTD";
        ws.Cells[2, 1, 2, kolonSayisi].Merge = true;
        ws.Cells[2, 1].Value = "EK MESAİ ÇİZELGESİ (BİRİM BAZLI)";
        ws.Cells[3, 1, 3, kolonSayisi].Merge = true;
        ws.Cells[3, 1].Value = $"Birim: {kapsamEtiketi}    |    " +
            $"Dönem: {new DateTime(yil, ay, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))}    |    " +
            $"Oluşturan: {HttpContext.Session.GetString("KullaniciAdi") ?? "-"}    |    " +
            $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}";

        ws.Cells[1, 1].Style.Font.Size = 12;
        ws.Cells[2, 1].Style.Font.Size = 14;
        ws.Cells[2, 1].Style.Font.Bold = true;
        ws.Cells[3, 1].Style.Font.Size = 9;
        ws.Cells[1, 1, 3, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        // Kolon başlıkları (satır 5)
        string[] basliklar = { "ADI SOYADI", "TARİH", "GÜN", "BAŞLANGIÇ", "BİTİŞ", "FİİLİ SAAT", "ZAM 0,1", "ZAM 0,5", "TOPLAM", "AÇIKLAMA" };
        for (var i = 0; i < basliklar.Length; i++)
            ws.Cells[5, i + 1].Value = basliklar[i];

        using (var baslikRange = ws.Cells[5, 1, 5, kolonSayisi])
        {
            baslikRange.Style.Font.Bold = true;
            baslikRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            baslikRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(30, 58, 138));
            baslikRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
            baslikRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }

        // Veri satırları (satır 6'dan itibaren)
        const int ilkVeriSatiri = 6;
        var satir = ilkVeriSatiri;
        foreach (DataRow k in mesailer.Rows)
        {
            ws.Cells[satir, 1].Value = k["AdSoyad"];
            ws.Cells[satir, 2].Value = k["Tarih"];
            ws.Cells[satir, 3].Value = k["Gun"];
            ws.Cells[satir, 4].Value = k["Baslangic"];
            ws.Cells[satir, 5].Value = k["Bitis"];
            ws.Cells[satir, 6].Value = k["FiiliSaat"];
            ws.Cells[satir, 7].Value = k["Zam01Saat"];
            ws.Cells[satir, 8].Value = k["Zam05Saat"];
            ws.Cells[satir, 9].Value = k["ToplamSaat"];
            ws.Cells[satir, 10].Value = k["Aciklama"];
            satir++;
        }

        // Sayı biçimi ve kenarlıklar
        if (satir > ilkVeriSatiri)
        {
            var veriAraligi = ws.Cells[ilkVeriSatiri, 1, satir - 1, kolonSayisi];
            veriAraligi.Style.Border.Top.Style = veriAraligi.Style.Border.Bottom.Style =
            veriAraligi.Style.Border.Left.Style = veriAraligi.Style.Border.Right.Style =
                OfficeOpenXml.Style.ExcelBorderStyle.Thin;

            var saatAraligi = ws.Cells[ilkVeriSatiri, 6, satir - 1, 9];
            saatAraligi.Style.Numberformat.Format = "#,##0.00";
            saatAraligi.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
        }

        // Genel toplam satırı
        if (satir > ilkVeriSatiri)
        {
            ws.Cells[satir, 5].Value = "GENEL TOPLAM";
            ws.Cells[satir, 5].Style.Font.Bold = true;
            ws.Cells[satir, 5].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
            for (var c = 6; c <= 9; c++)
            {
                decimal toplam = 0m;
                for (var s = ilkVeriSatiri; s < satir; s++)
                    toplam += Convert.ToDecimal(ws.Cells[s, c].Value);
                ws.Cells[satir, c].Value = toplam;
                ws.Cells[satir, c].Style.Font.Bold = true;
                ws.Cells[satir, c].Style.Numberformat.Format = "#,##0.00";
            }
        }

        ws.Cells[1, 1, satir, kolonSayisi].AutoFitColumns();

        // Hassas rapor yanıtı istemci/proxy önbelleğine alınmaz.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var personelKisaltma = personelNo.Length > 0 ? $"-{Uri.EscapeDataString(personelNo)}" : "";
        var dosyaAdi = $"ek-mesai-cizelgesi-{yil:D4}-{ay:D2}{personelKisaltma}.xlsx";
        return File(package.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", dosyaAdi);
    }

    private async Task<DataTable> GetMesaiRaporVerisiAsync(int yil, int ay, string birim, bool admin, string? personel = null)
    {
        var tablo = new DataTable("Mesai");
        tablo.Columns.Add("AdSoyad", typeof(string));
        tablo.Columns.Add("Tarih", typeof(string));
        tablo.Columns.Add("Gun", typeof(string));
        tablo.Columns.Add("Baslangic", typeof(string));
        tablo.Columns.Add("Bitis", typeof(string));
        tablo.Columns.Add("FiiliSaat", typeof(decimal));
        tablo.Columns.Add("Zam01Saat", typeof(decimal));
        tablo.Columns.Add("Zam05Saat", typeof(decimal));
        tablo.Columns.Add("ToplamSaat", typeof(decimal));
        tablo.Columns.Add("Aciklama", typeof(string));

        var personelNo = (personel ?? "").Trim();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var tumBirimler = admin && birim == "all";
        var filtreler = new List<string> { "YEAR(mk.tarih) = @yil", "MONTH(mk.tarih) = @ay" };
        if (!tumBirimler)
            filtreler.Insert(0, "p.birim_adi = @birim");
        if (personelNo.Length > 0)
            filtreler.Add("mk.personel_id = @personel");

        await using var cmd = new MySqlCommand(
            $@"SELECT CONCAT(p.ad, ' ', p.soyad), mk.tarih, mk.baslangic, mk.bitis,
                      mk.fiili_saat, mk.zam01_saat, mk.zam05_saat, mk.toplam_saat, mk.aciklama
               FROM mesai_kayitlari mk
               INNER JOIN personeller p ON p.per_no = mk.personel_id
               WHERE {string.Join(" AND ", filtreler)}
               ORDER BY p.ad, p.soyad, mk.tarih", connection);
        if (!tumBirimler)
            cmd.Parameters.AddWithValue("@birim", birim);
        if (personelNo.Length > 0)
            cmd.Parameters.AddWithValue("@personel", personelNo);
        cmd.Parameters.AddWithValue("@yil", yil);
        cmd.Parameters.AddWithValue("@ay", ay);

        await using var reader = await cmd.ExecuteReaderAsync();
        var culture = new System.Globalization.CultureInfo("tr-TR");
        while (await reader.ReadAsync())
        {
            var tarih = reader.GetDateTime(1);
            tablo.Rows.Add(
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                tarih.ToString("dd.MM.yyyy"),
                culture.DateTimeFormat.GetDayName(tarih.DayOfWeek),
                reader.GetTimeSpan(2).ToString(@"hh\:mm"),
                reader.GetTimeSpan(3).ToString(@"hh\:mm"),
                reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7),
                reader.IsDBNull(8) ? "" : reader.GetString(8));
        }

        return tablo;
    }

    /// Tablo ve kolon adları .frx ile birebir eşleşir: DataTable "Mesai" → ReferenceName "Data.Mesai".
    /// personel doluysa rapor yalnızca o personele ait kayıtlarla üretilir; başlıktaki Birim etiketine
    /// personel adı eklenir (şablon/değişken tasarımı aynı kalır).
    private (DataSet, Dictionary<string, object?>) MesaiRaporVerisiHazirla(DataTable mesailer, int yil, int ay, string birim, string? personel = null)
    {
        var ds = new DataSet("Data");
        ds.Tables.Add(mesailer);

        var personelNo = (personel ?? "").Trim();
        var birimEtiketi = birim == "all" ? "Tüm Personeller" : birim;
        string? personelEtiketi = null;
        if (personelNo.Length > 0)
        {
            // Filtreli tabloda tüm satırlar aynı personele aittir; adı ilk satırdan alırız.
            personelEtiketi = mesailer.Rows.Count > 0
                ? Convert.ToString(mesailer.Rows[0]["AdSoyad"])
                : personelNo;
            birimEtiketi = $"{birimEtiketi} · {personelEtiketi}";
        }

        var prm = new Dictionary<string, object?>
        {
            ["BaslikAdi"]   = "EK MESAİ ÇİZELGESİ (BİRİM BAZLI)",
            ["OlusturanAd"] = HttpContext.Session.GetString("KullaniciAdi") ?? "GAMABEL YATIRIM LTD",
            ["Tarih"]       = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            ["Birim"]       = birimEtiketi,
            ["Personel"]    = personelEtiketi,
            ["Ay"]          = new DateTime(yil, ay, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
            ["Yil"]         = yil,
            ["KayitSayisi"] = mesailer.Rows.Count,
            ["Filigran"]    = null, // Yalnızca taslak/onay durumu güvenilir şekilde belirlenebiliyorsa doldurun.
            // İmza isimleri: biliniyorsa doldurulur; boş bırakılırsa .frx'te yalnızca imza çizgisi görünür (elle imza).
            ["ImzaBirimSorumlusu"]  = "",
            ["ImzaBelediyeBaskani"] = ""
        };
        return (ds, prm);
    }

    public async Task<IActionResult> MesaiOdemesi()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
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

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
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

        if (yil <= 0 || ay < 1 || ay > 12)
        {
            return Json(new
            {
                basarili = false,
                hata = "Geçerli bir yıl ve ay seçiniz"
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
                $@"SELECT DISTINCT p.per_no, CONCAT(p.ad, ' ', p.soyad) AS ad_soyad, p.birim_adi,
                         COALESCE(SUM(mk.toplam_saat), 0) AS toplam_saat
                  FROM personeller p
                  INNER JOIN mesai_kayitlari mk ON mk.personel_id = p.per_no
                    AND YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
                  WHERE {birimFiltre}p.per_statu IS NOT NULL
                  GROUP BY p.per_no, p.ad, p.soyad, p.birim_adi
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
                var personelNo = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var adSoyad = reader.GetString(1);
                var birimAdi = reader.GetString(2);
                var toplamSaat = reader.GetDecimal(3);
                var toplamSaatGoruntuleme = Math.Round(toplamSaat, 2);
                
                // Hesapla parametresine göre ödeme hesapla veya 0 göster
                var odeme = hesapla ? Math.Round(toplamSaatGoruntuleme * brut, 2) : 0m;

                odemeler.Add(new
                {
                    sira,
                    personelId = personelNo,
                    adSoyad,
                    birimAdi,
                    toplamSaat = toplamSaatGoruntuleme,
                    saatlikBrut = Math.Round(brut, 2),
                    odemeTutari = Math.Round(odeme, 2),
                    saatlikBrutFormatted = brut.ToString("0.00"),
                    odemeTutariFormatted = odeme.ToString("0.00")
                });

                toplamMesai += toplamSaatGoruntuleme;
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
        public string PersonelId { get; set; } = "";
        public string PerNo { get; set; } = "";
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
