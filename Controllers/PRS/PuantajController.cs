using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Globalization;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Web;
using ClosedXML.Excel;
using MySqlConnector;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMenuPuantaj")]
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
                ? "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";

            await using var cmd = new MySqlCommand(sql, connection);
            if (!(rol == "admin" && birim == "all"))
                cmd.Parameters.AddWithValue("@birim", birim);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                personeller.Add(new
                {
                    perNo = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "",
                    ad = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    soyad = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    birim = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    statu = reader.IsDBNull(4) ? "" : reader.GetString(4)
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
                    personelId = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "",
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

    // ================================================================
    // ✅ İzin kaydet (tek veya toplu) - DÜZELTİLDİ
    // ================================================================
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

            // ✅ kilitli sütununu kontrol et
            await using var kilitCmd = new MySqlCommand(
                "SELECT kilitli FROM ay_kilitleri WHERE yil = @yil AND ay = @ay", kilitConn);
            kilitCmd.Parameters.AddWithValue("@yil", ilkIzin.Yil);
            kilitCmd.Parameters.AddWithValue("@ay", ilkIzin.Ay);

            var result = await kilitCmd.ExecuteScalarAsync();

            // ✅ Kayıt varsa ve kilitli = 1 ise kilitli
            if (result != null && Convert.ToInt32(result) == 1)
            {
                return Json(new { basarili = false, mesaj = "Bu ay kilitlenmiştir. Kayıt yapılamaz." });
            }
        }
        catch
        {
            // Tablo veya sütun yoksa kilit yok say
        }

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

    [HttpGet]
    public Task<IActionResult> Preview(int yil, int ay, string? birim, string? statu, string? arama) =>
        Pdf(yil, ay, birim, statu, arama, preview: true);

    [HttpGet]
    public Task<IActionResult> Excel(int yil, int ay, string? birim, string? statu, string? arama) =>
        Pdf(yil, ay, birim, statu, arama, excel: true);

    [HttpGet]
    public async Task<IActionResult> Pdf(int yil, int ay, string? birim, string? statu, string? arama, bool preview = false, bool excel = false)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");
        if (yil < 2000 || yil > 2100 || ay < 1 || ay > 12)
            return BadRequest("Geçersiz puantaj dönemi.");

        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
            birim = kullaniciBirim;
        else if (string.IsNullOrWhiteSpace(birim))
            birim = "all";

        var aramaMetni = (arama ?? "").Trim();
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var compareInfo = culture.CompareInfo;
        var personeller = new List<(string Id, string AdSoyad, string Birim, string Statu)>();
        var izinler = new Dictionary<(string PersonelId, int Gun), string>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var tumBirimler = rol == "admin" && birim == "all";
            var personelSql = tumBirimler
                ? "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";
            await using (var personelCmd = new MySqlCommand(personelSql, connection))
            {
                if (!tumBirimler)
                    personelCmd.Parameters.AddWithValue("@birim", birim ?? "");

                await using var reader = await personelCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                    var adSoyad = (reader.IsDBNull(1) ? "" : reader.GetString(1)) + " " +
                                  (reader.IsDBNull(2) ? "" : reader.GetString(2));
                    var personelBirim = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var personelStatu = reader.IsDBNull(4) ? "" : reader.GetString(4).Trim();

                    if (statu != null && statu != "__TUMU__" && personelStatu != statu)
                        continue;
                    if (!string.IsNullOrEmpty(aramaMetni) &&
                        compareInfo.IndexOf(adSoyad, aramaMetni, CompareOptions.IgnoreCase) < 0)
                        continue;

                    personeller.Add((id, adSoyad.Trim(), personelBirim, personelStatu));
                }
            }

            await using (var izinCmd = new MySqlCommand(
                "SELECT personel_id, gun, izin_tipi FROM puantaj_izin WHERE yil = @yil AND ay = @ay", connection))
            {
                izinCmd.Parameters.AddWithValue("@yil", yil);
                izinCmd.Parameters.AddWithValue("@ay", ay);
                await using var reader = await izinCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var personelId = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                    var gun = reader.GetInt32(1);
                    var izinTipi = reader.IsDBNull(2) ? "X" : reader.GetString(2);
                    if (gun is >= 1 and <= 30)
                        izinler[(personelId, gun)] = izinTipi;
                }
            }
        }
        catch
        {
            return Problem("Puantaj PDF verileri alınamadı.");
        }

        var table = new DataTable("Puantaj") { Locale = culture };
        table.Columns.Add("AdSoyad", typeof(string));
        table.Columns.Add("Birim", typeof(string));
        for (var gun = 1; gun <= 30; gun++)
            table.Columns.Add($"Gun{gun:D2}", typeof(string));
        table.Columns.Add("XSayisi", typeof(int));
        table.Columns.Add("ISayisi", typeof(int));
        table.Columns.Add("HSayisi", typeof(int));
        table.Columns.Add("Hakkedis", typeof(int));

        var izinTurleri = new HashSet<string>(new[] { "İ", "Gİ", "ÇH", "Vİ", "Öİ", "H", "Sİ", "ÖDİ" }, StringComparer.Ordinal);
        var genelX = 0;
        var genelI = 0;
        var genelH = 0;
        foreach (var personel in personeller)
        {
            var row = table.NewRow();
            row["AdSoyad"] = personel.AdSoyad;
            row["Birim"] = personel.Birim;
            var xSayisi = 0;
            var iSayisi = 0;
            var hSayisi = 0;

            for (var gun = 1; gun <= 30; gun++)
            {
                var durum = izinler.TryGetValue((personel.Id, gun), out var izinTipi) ? izinTipi : "X";
                row[$"Gun{gun:D2}"] = durum;
                if (durum == "X") xSayisi++;
                if (izinTurleri.Contains(durum)) iSayisi++;
                if (durum == "H") hSayisi++;
            }

            var hakkedis = 30 - iSayisi;
            row["XSayisi"] = xSayisi;
            row["ISayisi"] = iSayisi;
            row["HSayisi"] = hSayisi;
            row["Hakkedis"] = hakkedis;
            table.Rows.Add(row);
            genelX += xSayisi;
            genelI += iSayisi;
            genelH += hSayisi;
        }

        if (excel)
            return CreatePuantajExcel(table, yil, ay, birim, rol, personeller.Count, genelX, genelI, genelH);

        var reportPath = Path.Combine(AppContext.BaseDirectory, "Reports", "Puantaj", "Puantaj.frx");
        if (!System.IO.File.Exists(reportPath))
            reportPath = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "Puantaj", "Puantaj.frx");
        if (!System.IO.File.Exists(reportPath))
            return NotFound("Puantaj PDF şablonu bulunamadı.");

        var report = new Report();
        try
        {
            report.Load(reportPath);
            report.RegisterData(table, "Puantaj");
            report.GetDataSource("Puantaj")!.Enabled = true;
            report.SetParameterValue("Donem", $"{culture.DateTimeFormat.GetMonthName(ay)} {yil}");
            report.SetParameterValue("Birim", rol == "admin" && birim == "all" ? "Tüm Birimler" : birim ?? "");
            report.SetParameterValue("PersonelSayisi", personeller.Count);
            report.SetParameterValue("GenelXSayisi", genelX);
            report.SetParameterValue("GenelISayisi", genelI);
            report.SetParameterValue("GenelHSayisi", genelH);
            report.Prepare();

            if (preview)
            {
                var webReport = new WebReport { Report = report };
                webReport.Width = "100%";
                webReport.Height = "calc(100vh - 48px)";
                webReport.Inline = false;
                webReport.Toolbar.Show = true;
                webReport.Toolbar.ShowPrint = true;
                webReport.Toolbar.Exports.Show = true;
                webReport.Toolbar.Exports.ShowPreparedReport = true;
                return View("~/Views/PRS/Puantaj/Preview.cshtml", webReport);
            }

            await using var stream = new MemoryStream();
            using var export = new PDFSimpleExport();
            report.Export(export, stream);

            var safeBirim = new string((birim ?? "all").Where(char.IsLetterOrDigit).ToArray());
            var fileName = $"puantaj-{yil}-{ay:D2}-{(string.IsNullOrEmpty(safeBirim) ? "birim" : safeBirim)}.pdf";
            report.Dispose();
            return File(stream.ToArray(), "application/pdf", fileName);
        }
        catch
        {
            report.Dispose();
            return Problem("Puantaj PDF raporu oluşturulamadı.");
        }
    }

    private IActionResult CreatePuantajExcel(DataTable table, int yil, int ay, string? birim, string rol,
        int personelSayisi, int genelX, int genelI, int genelH)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Puantaj");
        var sonKolon = table.Columns.Count;
        var donem = $"{CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(ay)} {yil}";
        var birimAdi = rol == "admin" && birim == "all" ? "Tüm Birimler" : birim ?? "";

        sheet.Range(1, 1, 1, sonKolon).Merge();
        sheet.Cell(1, 1).Value = "PUANTAJ TAKVİMİ";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 15;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(1).Height = 28;

        sheet.Range(2, 1, 2, sonKolon).Merge();
        sheet.Cell(2, 1).Value = $"Dönem: {donem}    |    Birim: {birimAdi}";
        sheet.Cell(2, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var baslikSatiri = 4;
        sheet.Cell(baslikSatiri, 1).Value = "Adı Soyadı";
        sheet.Cell(baslikSatiri, 2).Value = "Birim";
        for (var gun = 1; gun <= 30; gun++)
            sheet.Cell(baslikSatiri, gun + 2).Value = gun.ToString("D2", CultureInfo.InvariantCulture);
        sheet.Cell(baslikSatiri, 33).Value = "X";
        sheet.Cell(baslikSatiri, 34).Value = "İ";
        sheet.Cell(baslikSatiri, 35).Value = "H";
        sheet.Cell(baslikSatiri, 36).Value = "Hakediş";

        var header = sheet.Range(baslikSatiri, 1, baslikSatiri, sonKolon);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var value = table.Rows[rowIndex][columnIndex];
                var cell = sheet.Cell(baslikSatiri + rowIndex + 1, columnIndex + 1);
                if (value is int intValue)
                    cell.Value = intValue;
                else
                    cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            }
        }

        var sonSatir = baslikSatiri + table.Rows.Count;
        if (sonSatir > baslikSatiri)
        {
            var body = sheet.Range(baslikSatiri + 1, 1, sonSatir, sonKolon);
            body.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            body.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Range(baslikSatiri + 1, 3, sonSatir, sonKolon).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(baslikSatiri + 1, 33, sonSatir, 36).Style.Font.Bold = true;
        }

        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 18;
        for (var column = 3; column <= 32; column++)
            sheet.Column(column).Width = 4.5;
        for (var column = 33; column <= 35; column++)
            sheet.Column(column).Width = 6;
        sheet.Column(36).Width = 10;
        sheet.SheetView.FreezeRows(baslikSatiri);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.PagesTall = 0;
        sheet.PageSetup.Margins.Top = 0.25;
        sheet.PageSetup.Margins.Bottom = 0.25;
        sheet.PageSetup.Margins.Left = 0.2;
        sheet.PageSetup.Margins.Right = 0.2;
        sheet.PageSetup.SetRowsToRepeatAtTop(baslikSatiri, baslikSatiri);

        var summaryRow = sonSatir + 2;
        sheet.Range(summaryRow, 1, summaryRow, sonKolon).Merge();
        sheet.Cell(summaryRow, 1).Value = $"Personel: {personelSayisi}    |    Toplam X: {genelX}    |    Toplam İ: {genelI}    |    Toplam H: {genelH}";
        sheet.Cell(summaryRow, 1).Style.Font.Bold = true;
        sheet.Cell(summaryRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EEF8");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var safeBirim = new string((birim ?? "all").Where(char.IsLetterOrDigit).ToArray());
        var fileName = $"puantaj-{yil}-{ay:D2}-{(string.IsNullOrEmpty(safeBirim) ? "birim" : safeBirim)}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}