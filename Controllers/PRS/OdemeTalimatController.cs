using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Web;
using gamabelmvc.Services;
using gamabelmvc.Models.PRS;
using System.Text.Json;
using System.Data;
using System.Globalization;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsAdmin")]
public class OdemeTalimatController : Controller
{
    private readonly OdemeFaturaImportService _importService;
    private readonly OdemeTalimatService _talimatService;
    private readonly IWebHostEnvironment _environment;

    public OdemeTalimatController(
        OdemeFaturaImportService importService,
        OdemeTalimatService talimatService,
        IWebHostEnvironment environment)
    {
        _importService = importService;
        _talimatService = talimatService;
        _environment = environment;
    }

    private bool IsLoggedIn() =>
        !string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi"));

    private string KullaniciAdi() =>
        HttpContext.Session.GetString("KullaniciAdi") ?? "";

    private bool IsAdmin() =>
        HttpContext.Session.GetString("Rol") == "admin";

    // -----------------------------------------------------------------------
    // ANA SAYFA – son yüklemeler ve talimatlar
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Index()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        ViewBag.KullaniciAdi = KullaniciAdi();
        ViewBag.IsAdmin = IsAdmin();

        try
        {
            ViewBag.SonYuklemeler = await _talimatService.GetSonYuklemelerAsync();
            ViewBag.SonTalimatlar = await _talimatService.GetSonTalimatlarAsync();
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
            ViewBag.SonYuklemeler = new List<OtImportBatch>();
            ViewBag.SonTalimatlar = new List<OtTalimat>();
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> FaturaAra(string? faturaNo, string? firmaAdi)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        var model = new FaturaAraViewModel
        {
            FaturaNo = faturaNo?.Trim() ?? string.Empty,
            FirmaAdi = firmaAdi?.Trim() ?? string.Empty,
            AramaYapildi = !string.IsNullOrWhiteSpace(faturaNo) || !string.IsNullOrWhiteSpace(firmaAdi)
        };

        if (model.FaturaNo.Length > 100 || model.FirmaAdi.Length > 250)
        {
            model.Hata = "Fatura numarası en fazla 100, firma adı en fazla 250 karakter olabilir.";
            return View(model);
        }

        if (!model.AramaYapildi)
            return View(model);

        try
        {
            var sonuclar = await _talimatService.FaturaAraAsync(model.FaturaNo, model.FirmaAdi);
            model.SonucSiniriUlasti = sonuclar.Count > 200;
            model.Sonuclar = sonuclar.Take(200).ToList();
        }
        catch (Exception ex)
        {
            model.Hata = "Fatura araması yapılamadı: " + ex.Message;
        }

        return View(model);
    }

    // -----------------------------------------------------------------------
    // EXCEL YÜKLEME
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yukle(IFormFile excelDosyasi)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (excelDosyasi == null || excelDosyasi.Length == 0)
        {
            TempData["Hata"] = "Lütfen bir Excel dosyası (.xlsx) seçin.";
            return RedirectToAction("Index");
        }

        if (!excelDosyasi.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Hata"] = "Sadece .xlsx uzantılı dosyalar yüklenebilir.";
            return RedirectToAction("Index");
        }

        try
        {
            await using var stream = excelDosyasi.OpenReadStream();
            var result = await _importService.ImportAsyncWithFilter(stream, excelDosyasi.FileName);
            var ozet = new ExcelYuklemeOzetViewModel
            {
                ExcelSatirSayisi = result.excelSatirSayisi,
                CiftIslemSayisi = result.ciftIslemSayisi,
                OdenmisIslemSayisi = result.odenmisIslemSayisi,
                MevcutAcikIslemSayisi = result.mevcutAcikIslemSayisi,
                Eklenen = result.eklenen,
                CiftFaturalar = result.ciftFaturalar,
                OdenmisFaturalar = result.odenmisFaturalar.Take(10).ToList(),
                EkOdenmisFaturaSayisi = Math.Max(0, result.odenmisFaturalar.Count - 10)
            };

            var batch = result.batch;
            if (batch == null)
            {
                TempData["YuklemeOzet"] = JsonSerializer.Serialize(ozet);
                return RedirectToAction("Index");
            }

            TempData["YuklemeOzet"] = JsonSerializer.Serialize(ozet);
            return RedirectToAction("FaturaSecim", new { batchId = batch.Id });
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Excel yükleme hatası: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    // -----------------------------------------------------------------------
    // FATURA SEÇİM
    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> FaturaSecim(int? batchId = null)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            List<OtFaturaViewModel> faturalar;
            
            if (batchId.HasValue && batchId.Value > 0)
            {
                faturalar = await _talimatService.GetBatchFaturalariWithFirmaAsync(batchId.Value);
                ViewBag.BatchId = batchId.Value;
                ViewBag.Baslik = $"Batch #{batchId} - Fatura Seçimi";
            }
            else
            {
                faturalar = await _talimatService.GetTumAcikFaturalarWithFirmaAsync();
                ViewBag.BatchId = 0;
                ViewBag.Baslik = "Tüm Açık Faturalar";
            }
            
            var bankalar = await _talimatService.GetBankalarAsync();

            var firmaGruplari = faturalar
                .GroupBy(f => f.CariKart)
                .Select(g => new 
                { 
                    FirmaAdi = g.Key,
                    OdemeIsmi = g.First().OdemeIsmi ?? "Ödeme adı bulunamadı",
                    IBAN = g.First().IBAN ?? "IBAN bulunamadı",
                    ToplamBakiye = g.Sum(f => f.Bakiye),
                    Faturalar = g.ToList()
                })
                .ToList();

            ViewBag.FirmaGruplari = firmaGruplari;
            ViewBag.Bankalar = bankalar;
            ViewBag.KullaniciAdi = KullaniciAdi();
            ViewBag.SecilenTalimatTarihi = TempData["SecilenTalimatTarihi"] is DateTime tarih
                ? tarih.Date
                : DateTime.Today;

            var secilenFaturaIdleri = new HashSet<int>();
            var secilenFaturaIdleriJson = TempData["SecilenFaturaIdleri"] as string;
            if (!string.IsNullOrWhiteSpace(secilenFaturaIdleriJson))
            {
                try
                {
                    var selectedIds = JsonSerializer.Deserialize<List<int>>(secilenFaturaIdleriJson);
                    if (selectedIds != null)
                        secilenFaturaIdleri = new HashSet<int>(selectedIds);
                }
                catch
                {
                    secilenFaturaIdleri = new HashSet<int>();
                }
            }

            ViewBag.SecilenFaturaIdleri = secilenFaturaIdleri;
            ViewBag.SecilenBankaId = TempData["SecilenBankaId"] is int id ? id : 0;

            if (!faturalar.Any())
            {
                if (batchId.HasValue && batchId.Value > 0)
                    TempData["Bilgi"] = "Bu yüklemede ödemeye dahil edilmemiş fatura kalmadı.";
                else
                    TempData["Bilgi"] = "Sistemde ödemeye hazır açık fatura bulunmuyor.";
            }
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Veri yükleme hatası: " + ex.Message;
            return RedirectToAction("Index");
        }

        return View();
    }

    // -----------------------------------------------------------------------
    // MANUEL KAYIT
    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ManuelKayit()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        await ManuelKayitSecenekleriniYukle();
        return View(new ManuelTalimatModel { TalimatTarihi = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManuelTalimatOlustur(ManuelTalimatModel model)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        if (model == null) return BadRequest();

        if (model.Satirlar == null || model.Satirlar.Count == 0)
            ModelState.AddModelError(nameof(model.Satirlar), "En az bir fatura satırı ekleyin.");

        var bankalar = await _talimatService.GetBankalarAsync();
        var firmalar = await _talimatService.GetFirmalarAsync();
        var banka = bankalar.FirstOrDefault(b => b.Id == model.BankaId);
        var firmaById = firmalar.ToDictionary(f => f.Id);

        if (banka == null)
            ModelState.AddModelError(nameof(model.BankaId), "Seçilen banka bulunamadı.");

        var satirlar = model.Satirlar ?? new List<ManuelTalimatSatiriModel>();
        var tutarBySatir = new Dictionary<ManuelTalimatSatiriModel, decimal>();
        for (var i = 0; i < satirlar.Count; i++)
        {
            var satir = satirlar[i];
            if (!firmaById.ContainsKey(satir.FirmaId))
                ModelState.AddModelError(nameof(model.Satirlar), "Bir veya daha fazla satırda geçersiz firma seçildi.");

            var tutarText = (satir.Tutar ?? string.Empty).Replace(',', '.');
            if (decimal.TryParse(tutarText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var tutar) &&
                tutar >= 0.01m && tutar <= 9999999999999999.99m)
            {
                tutarBySatir[satir] = tutar;
            }
            else
            {
                ModelState.AddModelError($"Satirlar[{i}].Tutar", "Tutar 0,01 ile 9.999.999.999.999.999,99 arasında olmalıdır.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Bankalar = bankalar;
            ViewBag.Firmalar = firmalar;
            return View("ManuelKayit", model);
        }

        var talimatSatirlari = satirlar
            .GroupBy(s => s.FirmaId)
            .Select(grup =>
            {
                var firma = firmaById[grup.Key];
                return new OtTalimatSatiri
                {
                    FirmaId = firma.Id,
                    FirmaOdemeIsmi = firma.OdemeIsmi,
                    FirmaIBAN = firma.IBAN,
                    Aciklama = string.Join(", ", grup.Select(s => "FATURA: " + s.FaturaAdi.Trim())),
                    Tutar = grup.Sum(s => tutarBySatir[s]),
                    AcikFaturaIdleri = new List<int>()
                };
            })
            .ToList();

        var geciciTalimat = new OtTalimat
        {
            Id = 0,
            TalimatNo = await _talimatService.GetSonrakiTalimatNoAsync(),
            Tarih = model.TalimatTarihi!.Value.Date,
            BankaId = banka!.Id,
            BankaSubeAdi = banka.SubeAdi,
            BankaIBAN = banka.IBAN,
            ToplamTutar = talimatSatirlari.Sum(s => s.Tutar),
            ToplamAdet = talimatSatirlari.Count,
            HazirlayanKullanici = KullaniciAdi(),
            Durum = "beklemede",
            Satirlar = talimatSatirlari
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        HttpContext.Session.SetString("GeciciTalimat", JsonSerializer.Serialize(geciciTalimat, jsonOptions));
        HttpContext.Session.SetString("GeciciFaturaIdleri", JsonSerializer.Serialize(new List<int>()));
        HttpContext.Session.SetInt32("GeciciBatchId", 0);

        TempData["Bilgi"] = "Manuel talimat oluşturuldu. Kaydetmek için 'Talimatı Kaydet' butonuna tıklayın.";
        return RedirectToAction("Detay", new { id = 0 });
    }

    private async Task ManuelKayitSecenekleriniYukle()
    {
        ViewBag.Bankalar = await _talimatService.GetBankalarAsync();
        ViewBag.Firmalar = await _talimatService.GetFirmalarAsync();
    }

    // -----------------------------------------------------------------------
    // TALİMAT OLUŞTUR - SADECE GEÇİCİ OLUŞTUR (KAYIT YOK)
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatOlustur(List<int> secilenFaturaIdleri, int bankaId, int batchId = 0, DateTime? talimatTarihi = null)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        var secilenTarih = talimatTarihi?.Date ?? DateTime.Today;
        TempData["SecilenTalimatTarihi"] = secilenTarih;

        if (secilenFaturaIdleri == null || secilenFaturaIdleri.Count == 0)
        {
            TempData["Hata"] = "Lütfen ödemeye dahil edilecek en az bir fatura seçin.";
            if (batchId > 0)
                return RedirectToAction("FaturaSecim", new { batchId });
            else
                return RedirectToAction("FaturaSecim");
        }

        try
        {
            var geciciTalimat = _talimatService.TalimatOlusturGecici(
                secilenFaturaIdleri, 
                bankaId, 
                KullaniciAdi(), 
                batchId,
                secilenTarih);

            if (geciciTalimat == null)
            {
                TempData["SecilenFaturaIdleri"] = JsonSerializer.Serialize(secilenFaturaIdleri);
                TempData["SecilenBankaId"] = bankaId;
                TempData["Hata"] = "Talimat oluşturulamadı.";
                if (batchId > 0)
                    return RedirectToAction("FaturaSecim", new { batchId });
                else
                    return RedirectToAction("FaturaSecim");
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var talimatJson = JsonSerializer.Serialize(geciciTalimat, options);
            var faturaIdJson = JsonSerializer.Serialize(secilenFaturaIdleri, options);
            
            // Session'a kaydet
            HttpContext.Session.SetString("GeciciTalimat", talimatJson);
            HttpContext.Session.SetString("GeciciFaturaIdleri", faturaIdJson);
            HttpContext.Session.SetInt32("GeciciBatchId", batchId);

            // TempData'ya da kaydet (yedek)
            TempData["GeciciTalimat"] = talimatJson;
            TempData["GeciciFaturaIdleri"] = faturaIdJson;
            TempData["GeciciBatchId"] = batchId;

            TempData["Bilgi"] = "Talimat oluşturuldu. Kaydetmek için 'Talimatı Kaydet' butonuna tıklayın.";
            return RedirectToAction("Detay", new { id = 0 });
        }
        catch (Exception ex)
        {
            TempData["SecilenFaturaIdleri"] = JsonSerializer.Serialize(secilenFaturaIdleri);
            TempData["SecilenBankaId"] = bankaId;
            TempData["Hata"] = "Talimat oluşturma hatası: " + ex.Message;
            if (batchId > 0)
                return RedirectToAction("FaturaSecim", new { batchId });
            else
                return RedirectToAction("FaturaSecim");
        }
    }

    // -----------------------------------------------------------------------
    // TALİMAT DETAY
    // -----------------------------------------------------------------------
   [HttpGet]
public async Task<IActionResult> Detay(int id)
{
    if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
    if (!IsAdmin()) return Forbid();

    if (id == 0)
    {
        // 1. Session'dan al
        var geciciTalimatJson = HttpContext.Session.GetString("GeciciTalimat");
        var geciciFaturaIdleriJson = HttpContext.Session.GetString("GeciciFaturaIdleri");
        var geciciBatchId = HttpContext.Session.GetInt32("GeciciBatchId") ?? 0;

        // 2. Session boşsa TempData'dan al ve korunmasını sağla
        if (string.IsNullOrEmpty(geciciTalimatJson))
        {
            geciciTalimatJson = TempData.Peek("GeciciTalimat") as string;
            geciciFaturaIdleriJson = TempData.Peek("GeciciFaturaIdleri") as string;
            geciciBatchId = TempData.Peek("GeciciBatchId") as int? ?? 0;
        }

        if (string.IsNullOrEmpty(geciciTalimatJson))
        {
            TempData["Hata"] = "Geçici talimat bulunamadı. Lütfen tekrar talimat oluşturun.";
            return RedirectToAction("Index");
        }

        try
        {
            // 🔧 JSON'u deserialize et - ÖZEL AYARLARLA
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // ⭐ EKSTRA: Null değerleri yoksay
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var geciciTalimat = JsonSerializer.Deserialize<OtTalimat>(geciciTalimatJson, options);
            
            if (geciciTalimat == null)
            {
                TempData["Hata"] = "Geçici talimat verisi bozuk.";
                return RedirectToAction("Index");
            }

            ViewBag.GeciciTalimatJson = geciciTalimatJson;
            ViewBag.GeciciFaturaIdleriJson = geciciFaturaIdleriJson;
            ViewBag.GeciciBankaId = geciciTalimat.BankaId;

            // ⭐ DEBUG: Satır sayısını kontrol et
            System.Diagnostics.Debug.WriteLine($"Detay - Satır Sayısı: {geciciTalimat.Satirlar?.Count ?? 0}");
            
            // Eğer satırlar boşsa, hata mesajı göster
            if (geciciTalimat.Satirlar == null || geciciTalimat.Satirlar.Count == 0)
            {
                // TempData ile uyarı göster
                TempData["Uyari"] = "Talimat oluşturuldu ancak fatura satırları bulunamadı. Lütfen tekrar deneyin.";
                // Yine de talimatı göster
            }

            // Session'ı güncelle
            HttpContext.Session.SetString("GeciciTalimat", JsonSerializer.Serialize(geciciTalimat, options));
            HttpContext.Session.SetString("GeciciFaturaIdleri", geciciFaturaIdleriJson ?? "[]");
            HttpContext.Session.SetInt32("GeciciBatchId", geciciBatchId);

            return View(geciciTalimat);
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Talimat verisi okunamadı: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    // ID > 0 ise veritabanından getir
    var talimat = await _talimatService.GetTalimatDetayAsync(id);
    if (talimat == null) return NotFound();

    return View(talimat);
}

    [HttpGet]
    public async Task<IActionResult> Rapor(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        if (id <= 0) return BadRequest("Rapor için kayıtlı bir talimat seçilmelidir.");

        var talimat = await _talimatService.GetTalimatDetayAsync(id);
        if (talimat == null) return NotFound();

        var reportPath = GetReportPath();
        if (!System.IO.File.Exists(reportPath))
            return NotFound($"Ödeme talimatı rapor şablonu bulunamadı: {reportPath}");

        var report = CreateReport(talimat, reportPath);
        var webReport = new WebReport { Report = report };
        webReport.Width = "100%";
        webReport.Height = "calc(100vh - 24px)";
        webReport.Inline = false;
        webReport.Toolbar.Show = true;
        webReport.Toolbar.ShowPrint = true;
        webReport.Toolbar.Exports.Show = true;
        webReport.Toolbar.Exports.ShowPreparedReport = true;
        return View("~/Views/PRS/OdemeTalimat/Rapor.cshtml", webReport);
    }

    [HttpGet]
    public async Task<IActionResult> RaporPdf(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        if (id <= 0) return BadRequest("Rapor için kayıtlı bir talimat seçilmelidir.");

        var talimat = await _talimatService.GetTalimatDetayAsync(id);
        if (talimat == null) return NotFound();

        var reportPath = GetReportPath();
        if (!System.IO.File.Exists(reportPath))
            return NotFound($"Ödeme talimatı rapor şablonu bulunamadı: {reportPath}");

        using var report = CreateReport(talimat, reportPath);
        await using var stream = new MemoryStream();
        using var export = new PDFSimpleExport();
        report.Export(export, stream);

        return File(stream.ToArray(), "application/pdf", $"odeme-talimat-{talimat.TalimatNo}.pdf");
    }

    [HttpGet]
    public IActionResult RaporPdf2(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        if (id <= 0) return BadRequest("PDF2 için kayıtlı bir talimat seçilmelidir.");

        return RedirectToAction(nameof(Rapor), new { id });
    }

    private string GetReportPath() =>
        Path.Combine(_environment.ContentRootPath, "Reports", "OdemeTalimat", "OdemeTalimat.frx");

    private static Report CreateReport(OtTalimat talimat, string reportPath)
    {
        var rows = new DataTable("OdemeTalimat");
        rows.Columns.Add("Sira", typeof(int));
        rows.Columns.Add("FirmaOdemeIsmi", typeof(string));
        rows.Columns.Add("FirmaIBAN", typeof(string));
        rows.Columns.Add("Aciklama", typeof(string));
        rows.Columns.Add("Tutar", typeof(decimal));

        for (var index = 0; index < talimat.Satirlar.Count; index++)
        {
            var row = talimat.Satirlar[index];
            rows.Rows.Add(index + 1, row.FirmaOdemeIsmi, row.FirmaIBAN, row.Aciklama, row.Tutar);
        }

        var report = new Report();
        report.Load(reportPath);
        report.RegisterData(rows, "OdemeTalimat");
        report.GetDataSource("OdemeTalimat")!.Enabled = true;
        report.SetParameterValue("TalimatNo", talimat.TalimatNo);
        report.SetParameterValue("Tarih", talimat.Tarih.ToString("dd.MM.yyyy"));
        report.SetParameterValue("BankaIBAN", talimat.BankaIBAN);
        report.SetParameterValue("BankaSubeAdi", talimat.BankaSubeAdi);
        report.SetParameterValue("ToplamAdet", talimat.ToplamAdet);
        report.SetParameterValue("ToplamTutar", talimat.ToplamTutar.ToString("N2"));
        report.Prepare();
        return report;
    }

    // -----------------------------------------------------------------------
    // TALİMAT KAYDET (VERİTABANINA KAYIT)
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatKaydet(int id, string? GeciciTalimatJson = null, string? GeciciFaturaIdleriJson = null, int? GeciciBankaId = null)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            // Önce Session'dan dene
            var geciciTalimatJson = HttpContext.Session.GetString("GeciciTalimat");
            var geciciFaturaIdleriJson = HttpContext.Session.GetString("GeciciFaturaIdleri");
            var geciciBatchId = HttpContext.Session.GetInt32("GeciciBatchId") ?? 0;

            // Session boşsa TempData'dan al
            if (string.IsNullOrEmpty(geciciTalimatJson))
            {
                geciciTalimatJson = TempData["GeciciTalimat"] as string;
                geciciFaturaIdleriJson = TempData["GeciciFaturaIdleri"] as string;
                geciciBatchId = TempData["GeciciBatchId"] as int? ?? 0;
            }

            if (string.IsNullOrEmpty(geciciTalimatJson) && !string.IsNullOrEmpty(GeciciTalimatJson))
                geciciTalimatJson = GeciciTalimatJson;

            if (string.IsNullOrEmpty(geciciFaturaIdleriJson) && !string.IsNullOrEmpty(GeciciFaturaIdleriJson))
                geciciFaturaIdleriJson = GeciciFaturaIdleriJson;

            if (string.IsNullOrEmpty(geciciTalimatJson) || string.IsNullOrEmpty(geciciFaturaIdleriJson))
            {
                TempData["Hata"] = "Geçici talimat bulunamadı.";
                return RedirectToAction("Index");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var geciciTalimat = JsonSerializer.Deserialize<OtTalimat>(geciciTalimatJson, jsonOptions);
            var secilenFaturaIdleri = JsonSerializer.Deserialize<List<int>>(geciciFaturaIdleriJson, jsonOptions);

            if (geciciTalimat != null && geciciTalimat.BankaId == 0 && GeciciBankaId.HasValue)
                geciciTalimat.BankaId = GeciciBankaId.Value;

            // Seçilen bankanın hâlâ veritabanında mevcut olduğunu kontrol et
            var bankalar = await _talimatService.GetBankalarAsync();
            if (geciciTalimat == null || !bankalar.Any(b => b.Id == geciciTalimat.BankaId))
            {
                TempData["Hata"] = $"Seçilen banka (id: {geciciTalimat?.BankaId ?? 0}) bulunamadı. Lütfen bankayı yeniden seçin veya talimatı yeniden oluşturun.";
                return RedirectToAction("Detay", new { id = 0 });
            }

            if (geciciTalimat == null || secilenFaturaIdleri == null)
            {
                TempData["Hata"] = "Geçici talimat verisi bozuk.";
                return RedirectToAction("Index");
            }

            var kaydedilenTalimat = await _talimatService.TalimatKaydetAsync(
                geciciTalimat,
                secilenFaturaIdleri,
                geciciBatchId);

            if (kaydedilenTalimat == null)
            {
                TempData["Hata"] = "Talimat kaydedilemedi.";
                return RedirectToAction("Detay", new { id = 0 });
            }

            // Session ve TempData temizle
            HttpContext.Session.Remove("GeciciTalimat");
            HttpContext.Session.Remove("GeciciFaturaIdleri");
            HttpContext.Session.Remove("GeciciBatchId");
            TempData.Remove("GeciciTalimat");
            TempData.Remove("GeciciFaturaIdleri");
            TempData.Remove("GeciciBatchId");

            TempData["Basarili"] = $"Talimat #{kaydedilenTalimat.TalimatNo} başarıyla kaydedildi!";
            return RedirectToAction("Detay", new { id = kaydedilenTalimat.Id });
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Talimat kaydetme hatası: " + ex.Message;
            return RedirectToAction("Detay", new { id = 0 });
        }
    }

    // -----------------------------------------------------------------------
    // FİRMA YÖNETİMİ
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Firmalar()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        ViewBag.KullaniciAdi = KullaniciAdi();
        var firmalar = await _talimatService.GetFirmalarAsync();
        return View(firmalar);
    }

    [HttpGet]
    public async Task<IActionResult> FirmalarRapor(string yon = "dikey")
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (!TryGetFirmalarReportOrientation(yon, out var landscape))
            return BadRequest("Geçersiz rapor yönü. Geçerli değerler: dikey | yatay");

        var reportPath = GetFirmalarReportPath(landscape);
        if (!System.IO.File.Exists(reportPath))
            return NotFound("Firma rapor şablonu bulunamadı.");

        var firmalar = await _talimatService.GetFirmalarAsync();
        var report = CreateFirmalarReport(firmalar, reportPath);

        var webReport = new WebReport { Report = report };
        webReport.Width = "100%";
        webReport.Height = "calc(100vh - 48px)";
        webReport.Inline = false;
        webReport.Toolbar.Show = true;
        webReport.Toolbar.ShowPrint = true;
        webReport.Toolbar.Exports.Show = true;
        webReport.Toolbar.Exports.ShowPreparedReport = true;

        ViewBag.Yon = landscape ? "yatay" : "dikey";
        return View("~/Views/PRS/OdemeTalimat/FirmalarRapor.cshtml", webReport);
    }

    [HttpGet]
    public async Task<IActionResult> FirmalarRaporPdf(string yon = "dikey")
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (!TryGetFirmalarReportOrientation(yon, out var landscape))
            return BadRequest("Geçersiz rapor yönü. Geçerli değerler: dikey | yatay");

        var reportPath = GetFirmalarReportPath(landscape);
        if (!System.IO.File.Exists(reportPath))
            return NotFound("Firma rapor şablonu bulunamadı.");

        var firmalar = await _talimatService.GetFirmalarAsync();
        using var report = CreateFirmalarReport(firmalar, reportPath);
        await using var stream = new MemoryStream();
        using var export = new PDFSimpleExport();
        report.Export(export, stream);

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        return File(stream.ToArray(), "application/pdf", $"odeme-talimat-firmalar-{(landscape ? "yatay" : "dikey")}.pdf");
    }

    private static bool TryGetFirmalarReportOrientation(string? yon, out bool landscape)
    {
        if (string.Equals(yon?.Trim(), "dikey", StringComparison.OrdinalIgnoreCase))
        {
            landscape = false;
            return true;
        }

        if (string.Equals(yon?.Trim(), "yatay", StringComparison.OrdinalIgnoreCase))
        {
            landscape = true;
            return true;
        }

        landscape = false;
        return false;
    }

    private string GetFirmalarReportPath(bool landscape) =>
        Path.Combine(_environment.ContentRootPath, "Reports", "OdemeTalimat",
            $"Firmalar.{(landscape ? "Yatay" : "Dikey")}.frx");

    private static Report CreateFirmalarReport(List<OtFirma> firmalar, string reportPath)
    {
        var rows = new DataTable("Firmalar");
        rows.Columns.Add("CariIsmi", typeof(string));
        rows.Columns.Add("OdemeIsmi", typeof(string));
        rows.Columns.Add("IBAN", typeof(string));
        rows.Columns.Add("EmailIletisim", typeof(string));
        rows.Columns.Add("Aciklama", typeof(string));

        if (firmalar.Count == 0)
        {
            rows.Rows.Add("Kayıtlı firma bulunamadı.", string.Empty, string.Empty, string.Empty, string.Empty);
        }
        else
        {
            foreach (var firma in firmalar)
            {
                var email = string.IsNullOrWhiteSpace(firma.Email) ? string.Empty : firma.Email.Trim();
                var cc = string.IsNullOrWhiteSpace(firma.EmailCc) ? string.Empty : $"CC: {firma.EmailCc.Trim()}";
                var emailIletisim = string.Join(Environment.NewLine, new[] { email, cc }.Where(x => x.Length > 0));
                rows.Rows.Add(firma.CariIsmi, firma.OdemeIsmi, firma.IBAN, emailIletisim, firma.Aciklama ?? string.Empty);
            }
        }

        var report = new Report();
        report.Load(reportPath);
        report.RegisterData(rows, "Firmalar");
        var dataSource = report.GetDataSource("Firmalar")
            ?? throw new InvalidOperationException("Firma rapor şablonunda 'Firmalar' veri kaynağı bulunamadı.");
        dataSource.Enabled = true;
        report.SetParameterValue("RaporTarihi", DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")));
        report.SetParameterValue("FirmaSayisi", firmalar.Count);
        report.Prepare();
        return report;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirmaKaydet(OtFirma firma)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (string.IsNullOrWhiteSpace(firma.CariIsmi) || string.IsNullOrWhiteSpace(firma.IBAN))
        {
            TempData["Hata"] = "Cari ismi ve IBAN zorunludur.";
            return RedirectToAction("Firmalar");
        }

        try
        {
            await _talimatService.FirmaKaydetAsync(firma);
            TempData["Basarili"] = "Firma başarıyla kaydedildi.";
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Firma kaydedilemedi: " + ex.Message;
        }
        return RedirectToAction("Firmalar");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirmaSil(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        
        try
        {
            await _talimatService.FirmaSilAsync(id);
            TempData["Basarili"] = "Firma başarıyla silindi.";
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Firma silinemedi: " + ex.Message;
        }
        return RedirectToAction("Firmalar");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatSatiriSil(int id, int satirId)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            var updatedTalimat = await _talimatService.TalimatSatiriSilAsync(id, satirId);
            TempData["Basarili"] = updatedTalimat != null
                ? $"Talimat satırı başarıyla silindi. Yeni toplam: {updatedTalimat.ToplamAdet} ödeme / {updatedTalimat.ToplamTutar:N2} TL"
                : "Talimat satırı silindi.";
            return RedirectToAction("Detay", new { id });
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Ödeme satırı silinemedi: " + ex.Message;
            return RedirectToAction("Detay", new { id });
        }
    }

    // -----------------------------------------------------------------------
    // BANKA YÖNETİMİ
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Bankalar()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        ViewBag.KullaniciAdi = KullaniciAdi();
        var bankalar = await _talimatService.GetBankalarAsync();
        return View(bankalar);
    }

    [HttpGet]
    public async Task<IActionResult> BankalarRapor()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        var reportPath = GetBankalarReportPath();
        if (!System.IO.File.Exists(reportPath))
            return NotFound("Banka rapor şablonu bulunamadı.");

        var bankalar = await _talimatService.GetBankalarAsync();
        var report = CreateBankalarReport(bankalar, reportPath);
        var webReport = new WebReport { Report = report };
        webReport.Width = "100%";
        webReport.Height = "calc(100vh - 48px)";
        webReport.Inline = false;
        webReport.Toolbar.Show = true;
        webReport.Toolbar.ShowPrint = true;
        webReport.Toolbar.Exports.Show = true;
        webReport.Toolbar.Exports.ShowPreparedReport = true;

        return View("~/Views/PRS/OdemeTalimat/BankalarRapor.cshtml", webReport);
    }

    [HttpGet]
    public async Task<IActionResult> BankalarRaporPdf()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        var reportPath = GetBankalarReportPath();
        if (!System.IO.File.Exists(reportPath))
            return NotFound("Banka rapor şablonu bulunamadı.");

        var bankalar = await _talimatService.GetBankalarAsync();
        using var report = CreateBankalarReport(bankalar, reportPath);
        await using var stream = new MemoryStream();
        using var export = new PDFSimpleExport();
        report.Export(export, stream);

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        return File(stream.ToArray(), "application/pdf", "odeme-talimat-bankalar-yatay.pdf");
    }

    private string GetBankalarReportPath() =>
        Path.Combine(_environment.ContentRootPath, "Reports", "OdemeTalimat", "Bankalar.Yatay.frx");

    private static Report CreateBankalarReport(List<OtBanka> bankalar, string reportPath)
    {
        var rows = new DataTable("Bankalar");
        rows.Columns.Add("SubeAdi", typeof(string));
        rows.Columns.Add("IBAN", typeof(string));

        if (bankalar.Count == 0)
        {
            rows.Rows.Add("Kayıtlı banka/hesap bulunamadı.", string.Empty);
        }
        else
        {
            foreach (var banka in bankalar)
                rows.Rows.Add(banka.SubeAdi, banka.IBAN);
        }

        var report = new Report();
        report.Load(reportPath);
        report.RegisterData(rows, "Bankalar");
        var dataSource = report.GetDataSource("Bankalar")
            ?? throw new InvalidOperationException("Banka rapor şablonunda 'Bankalar' veri kaynağı bulunamadı.");
        dataSource.Enabled = true;
        report.SetParameterValue("RaporTarihi", DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")));
        report.SetParameterValue("BankaSayisi", bankalar.Count);
        report.Prepare();
        return report;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BankaKaydet(OtBanka banka)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (string.IsNullOrWhiteSpace(banka.SubeAdi) || string.IsNullOrWhiteSpace(banka.IBAN))
        {
            TempData["Hata"] = "Şube adı ve IBAN zorunludur.";
            return RedirectToAction("Bankalar");
        }

        try
        {
            await _talimatService.BankaKaydetAsync(banka);
            TempData["Basarili"] = "Banka başarıyla kaydedildi.";
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Banka kaydedilemedi: " + ex.Message;
        }
        return RedirectToAction("Bankalar");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BankaSil(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();
        
        try
        {
            await _talimatService.BankaSilAsync(id);
            TempData["Basarili"] = "Banka başarıyla silindi.";
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Banka silinemedi: " + ex.Message;
        }
        return RedirectToAction("Bankalar");
    }
}