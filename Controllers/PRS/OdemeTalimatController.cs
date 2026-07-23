using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Services;
using gamabelmvc.Models.PRS;
using System.Text.Json;

namespace gamabelmvc.Controllers.PRS;

public class OdemeTalimatController : Controller
{
    private readonly OdemeFaturaImportService _importService;
    private readonly OdemeTalimatService _talimatService;

    public OdemeTalimatController(
        OdemeFaturaImportService importService,
        OdemeTalimatService talimatService)
    {
        _importService = importService;
        _talimatService = talimatService;
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
            var batch = await _importService.ImportAsync(stream, excelDosyasi.FileName);
            
            TempData["Basarili"] = $"✅ Excel dosyası başarıyla yüklendi! {batch.SatirSayisi} fatura kaydedildi.";
            
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
    // TALİMAT OLUŞTUR - SADECE GEÇİCİ OLUŞTUR (KAYIT YOK)
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatOlustur(List<int> secilenFaturaIdleri, int bankaId, int batchId = 0)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

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
                batchId);

            if (geciciTalimat == null)
            {
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

        // 2. Session boşsa TempData'dan al
        if (string.IsNullOrEmpty(geciciTalimatJson))
        {
            geciciTalimatJson = TempData["GeciciTalimat"] as string;
            geciciFaturaIdleriJson = TempData["GeciciFaturaIdleri"] as string;
            geciciBatchId = TempData["GeciciBatchId"] as int? ?? 0;
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

    // -----------------------------------------------------------------------
    // TALİMAT KAYDET (VERİTABANINA KAYIT)
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatKaydet(int id)
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

            if (string.IsNullOrEmpty(geciciTalimatJson) || string.IsNullOrEmpty(geciciFaturaIdleriJson))
            {
                TempData["Hata"] = "Geçici talimat bulunamadı.";
                return RedirectToAction("Index");
            }

            var geciciTalimat = JsonSerializer.Deserialize<OtTalimat>(geciciTalimatJson);
            var secilenFaturaIdleri = JsonSerializer.Deserialize<List<int>>(geciciFaturaIdleriJson);

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