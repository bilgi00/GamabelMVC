using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Services;
using gamabelmvc.Models.PRS;

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
    public async Task<IActionResult> FaturaSecim(int batchId)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            var faturalar = await _talimatService.GetBatchFaturalariAsync(batchId);
            var bankalar = await _talimatService.GetBankalarAsync();

            ViewBag.BatchId = batchId;
            ViewBag.FirmaGruplari = faturalar.GroupBy(f => f.CariKart).ToList();
            ViewBag.Bankalar = bankalar;
            ViewBag.KullaniciAdi = KullaniciAdi();

            if (!faturalar.Any())
                TempData["Bilgi"] = "Bu yüklemede henüz ödemeye dahil edilmemiş fatura kalmadı.";
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Veri yükleme hatası: " + ex.Message;
            return RedirectToAction("Index");
        }

        return View();
    }

    // -----------------------------------------------------------------------
    // TALİMAT OLUŞTUR
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TalimatOlustur(int batchId, List<int> secilenFaturaIdleri, int bankaId)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        if (secilenFaturaIdleri == null || secilenFaturaIdleri.Count == 0)
        {
            TempData["Hata"] = "Lütfen ödemeye dahil edilecek en az bir fatura seçin.";
            return RedirectToAction("FaturaSecim", new { batchId });
        }

        try
        {
            var talimat = await _talimatService.TalimatOlusturAsync(
                batchId, secilenFaturaIdleri, bankaId, KullaniciAdi());
            return RedirectToAction("Detay", new { id = talimat.Id });
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Talimat oluşturma hatası: " + ex.Message;
            return RedirectToAction("FaturaSecim", new { batchId });
        }
    }

    // -----------------------------------------------------------------------
    // TALİMAT DETAY / YAZDIR
    // -----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Detay(int id)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        var talimat = await _talimatService.GetTalimatDetayAsync(id);
        if (talimat == null) return NotFound();

        return View(talimat);
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
        await _talimatService.FirmaSilAsync(id);
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
        await _talimatService.BankaSilAsync(id);
        return RedirectToAction("Bankalar");
    }
}
