using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Utils;
using FastReport.Web;
using System.Data;
using System.Drawing;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMenuMesai")]
public class MesaiController : Controller
{
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;

    public MesaiController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
        _environment = environment;
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

    [HttpGet]
    public async Task<IActionResult> FastReportPreview(int yil, int ay, string birim, bool personelBazli = false)
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

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin");
        var reportFileName = personelBazli ? "EkMesaiPersonelBazli.frx" : "EkMesaiCizelgesi.frx";
        var reportPath = Path.Combine(_environment.ContentRootPath, "Reports", "Mesai", reportFileName);
        if (!System.IO.File.Exists(reportPath))
            return NotFound($"FastReport şablonu bulunamadı: {reportPath}");

        var webReport = new WebReport();
        webReport.Report.Load(reportPath);
        webReport.Report.RegisterData(mesailer, "Mesai");
        webReport.Report.GetDataSource("Mesai")!.Enabled = true;
        webReport.Report.SetParameterValue("Birim", birim == "all" ? "Tüm Personeller" : birim);
        webReport.Report.SetParameterValue("Ay", new DateTime(yil, ay, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")));
        webReport.Report.SetParameterValue("Yil", yil);
        webReport.Report.Prepare();
        webReport.Toolbar.Show = true;

        return View("~/Views/PRS/Mesai/FastReportPreview.cshtml", webReport);
    }

    [HttpGet]
    public async Task<IActionResult> FastReportPdf(int yil, int ay, string birim, bool personelBazli = false)
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

        var mesailer = await GetMesaiRaporVerisiAsync(yil, ay, birim, rol == "admin");
        var reportFileName = personelBazli ? "EkMesaiPersonelBazli.frx" : "EkMesaiCizelgesi.frx";
        var reportPath = Path.Combine(_environment.ContentRootPath, "Reports", "Mesai", reportFileName);
        if (!System.IO.File.Exists(reportPath))
            return NotFound($"FastReport şablonu bulunamadı: {reportPath}");

        using var report = new Report();
        report.Load(reportPath);
        report.RegisterData(mesailer, "Mesai");
        report.GetDataSource("Mesai")!.Enabled = true;
        report.SetParameterValue("Birim", birim == "all" ? "Tüm Personeller" : birim);
        report.SetParameterValue("Ay", new DateTime(yil, ay, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")));
        report.SetParameterValue("Yil", yil);
        report.Prepare();

        await using var stream = new MemoryStream();
        using var export = new PDFSimpleExport();
        report.Export(export, stream);

        var dosyaAdi = $"mesai-{(personelBazli ? "personel" : "birim")}-{yil:D4}-{ay:D2}.pdf";
        return File(stream.ToArray(), "application/pdf", dosyaAdi);
    }

    private async Task<DataTable> GetMesaiRaporVerisiAsync(int yil, int ay, string birim, bool admin)
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

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var tumBirimler = admin && birim == "all";
        var filtre = tumBirimler ? "" : "p.birim_adi = @birim AND ";
        await using var cmd = new MySqlCommand(
            $@"SELECT CONCAT(p.ad, ' ', p.soyad), mk.tarih, mk.baslangic, mk.bitis,
                      mk.fiili_saat, mk.zam01_saat, mk.zam05_saat, mk.toplam_saat, mk.aciklama
               FROM mesai_kayitlari mk
               INNER JOIN personeller p ON p.per_no = mk.personel_id
               WHERE {filtre}YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
               ORDER BY p.ad, p.soyad, mk.tarih", connection);
        if (!tumBirimler)
            cmd.Parameters.AddWithValue("@birim", birim);
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

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Report CreateMesaiReport(DataTable mesailer, string baslik, string birim, string ayAdi, int yil, bool personelBazli)
    {
        var report = new Report();
        report.RegisterData(mesailer, "Mesai");
        report.GetDataSource("Mesai")!.Enabled = true;

        var page = new ReportPage { Name = "MesaiRaporu", PaperWidth = 297, PaperHeight = 210 };
        report.Pages.Add(page);

        page.ReportTitle = new ReportTitleBand { Name = "Baslik", Height = Units.Millimeters * 19 };
        var kurum = CreateText("Kurum", "GAMABEL YATIRIM LTD", 0, 0, 277, 6, 12, true, HorzAlign.Center);
        kurum.Fill = new SolidFill(Color.FromArgb(30, 60, 114));
        kurum.TextColor = Color.White;
        kurum.Border.Color = Color.FromArgb(30, 60, 114);
        page.ReportTitle.Objects.Add(kurum);

        var raporBaslik = CreateText("RaporBaslik", baslik, 0, 6, 277, 6, 10, true, HorzAlign.Center);
        raporBaslik.Fill = new SolidFill(Color.FromArgb(233, 238, 248));
        raporBaslik.Border.Color = Color.FromArgb(30, 60, 114);
        page.ReportTitle.Objects.Add(raporBaslik);

        var raporBilgi = CreateText("RaporBilgi", $"Birim: {birim}  |  Ay: {ayAdi}  |  Yıl: {yil}", 0, 12, 277, 5, 8, false, HorzAlign.Center);
        raporBilgi.Fill = new SolidFill(Color.FromArgb(248, 250, 252));
        raporBilgi.Border.Color = Color.FromArgb(210, 218, 230);
        page.ReportTitle.Objects.Add(raporBilgi);

        page.PageHeader = new PageHeaderBand { Name = "TabloBaslik", Height = Units.Millimeters * 7 };
        var columns = new[]
        {
            ("Adı Soyadı", "AdSoyad", 40f), ("Tarih", "Tarih", 22f), ("Gün", "Gun", 21f),
            ("Başlangıç", "Baslangic", 20f), ("Bitiş", "Bitis", 20f), ("Fiili", "FiiliSaat", 18f),
            ("Zam 0.1", "Zam01Saat", 18f), ("Zam 0.5", "Zam05Saat", 18f),
            ("Toplam", "ToplamSaat", 18f), ("Açıklama", "Aciklama", 62f), ("İmza", "", 20f)
        };
        var left = 0f;
        foreach (var (caption, _, width) in columns)
        {
            var header = CreateText($"H{left}", caption, left, 0, width, 7, 7, true, HorzAlign.Center);
            header.Fill = new SolidFill(Color.FromArgb(30, 60, 114));
            header.TextColor = Color.White;
            header.Border.Color = Color.White;
            page.PageHeader.Objects.Add(header);
            left += width;
        }

        var data = new DataBand { Name = "MesaiSatirlari", Height = Units.Millimeters * 6, DataSource = report.GetDataSource("Mesai") };
        if (personelBazli)
        {
            var group = new GroupHeaderBand { Name = "PersonelGrup", Height = Units.Millimeters * 6, Condition = "[Mesai.AdSoyad]" };
            var personelAdi = CreateText("PersonelAdi", "[Mesai.AdSoyad]", 0, 0, 277, 6, 8, true, HorzAlign.Left);
            personelAdi.Fill = new SolidFill(Color.FromArgb(219, 229, 243));
            personelAdi.Border.Color = Color.FromArgb(30, 60, 114);
            group.Objects.Add(personelAdi);
            group.Data = data;
            page.Bands.Add(group);
        }
        else
        {
            page.Bands.Add(data);
        }

        left = 0;
        foreach (var (_, field, width) in columns)
        {
            var value = string.IsNullOrEmpty(field) ? "" : $"[Mesai.{field}]";
            var alignment = field is "FiiliSaat" or "Zam01Saat" or "Zam05Saat" or "ToplamSaat" ? HorzAlign.Right : HorzAlign.Left;
            data.Objects.Add(CreateText($"D{left}", value, left, 0, width, 6, 7, false, alignment));
            left += width;
        }

        page.ReportSummary = new ReportSummaryBand { Name = "RaporOzeti", Height = Units.Millimeters * 35 };
        var fiili = mesailer.AsEnumerable().Sum(row => row.Field<decimal>("FiiliSaat"));
        var zam01 = mesailer.AsEnumerable().Sum(row => row.Field<decimal>("Zam01Saat"));
        var zam05 = mesailer.AsEnumerable().Sum(row => row.Field<decimal>("Zam05Saat"));
        var toplam = mesailer.AsEnumerable().Sum(row => row.Field<decimal>("ToplamSaat"));

        var ozet = CreateText("Ozet", $"GENEL TOPLAM    Fiili: {fiili:0.00} saat   |   Zam 0.1: {zam01:0.00}   |   Zam 0.5: {zam05:0.00}   |   TOPLAM: {toplam:0.00} saat", 0, 2, 277, 7, 8, true, HorzAlign.Center);
        ozet.Fill = new SolidFill(Color.FromArgb(219, 229, 243));
        ozet.Border.Color = Color.FromArgb(30, 60, 114);
        page.ReportSummary.Objects.Add(ozet);

        var birimImza = CreateText("BirimImza", "BİRİM SORUMLUSU\n\nAdı Soyadı / İmza", 0, 13, 130, 18, 8, true, HorzAlign.Center);
        birimImza.Border.Color = Color.FromArgb(120, 130, 145);
        page.ReportSummary.Objects.Add(birimImza);

        var baskanImza = CreateText("BaskanImza", "BELEDİYE BAŞKANI\n\nAdı Soyadı / İmza", 147, 13, 130, 18, 8, true, HorzAlign.Center);
        baskanImza.Border.Color = Color.FromArgb(120, 130, 145);
        page.ReportSummary.Objects.Add(baskanImza);

        page.PageFooter = new PageFooterBand { Name = "AltBilgi", Height = Units.Millimeters * 5 };
        page.PageFooter.Objects.Add(CreateText("Sayfa", "Sayfa [PageN]", 0, 0, 277, 5, 7, false, HorzAlign.Right));
        return report;
    }

    private static TextObject CreateText(string name, string text, float leftMm, float topMm, float widthMm, float heightMm, float fontSize, bool bold, HorzAlign align)
    {
        var cell = new TextObject
        {
            Name = name,
            Bounds = new RectangleF(leftMm * Units.Millimeters, topMm * Units.Millimeters, widthMm * Units.Millimeters, heightMm * Units.Millimeters),
            Text = text,
            Font = new Font("Arial", fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
            HorzAlign = align,
            VertAlign = VertAlign.Center
        };
        cell.Border.Lines = BorderLines.All;
        return cell;
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
