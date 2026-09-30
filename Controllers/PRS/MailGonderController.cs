// Controllers/PRS/MailGonderController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using gamabelmvc.Services;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMailAdmin")]
public class MailGonderController : Controller
{
    private readonly OdemeTalimatService _talimatService;
    private readonly IMailService _mailService;
    private readonly MailQueueService _mailQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MailGonderController> _logger;

    public MailGonderController(
        OdemeTalimatService talimatService,
        IMailService mailService,
        MailQueueService mailQueue,
        IConfiguration configuration,
        ILogger<MailGonderController> logger)
    {
        _talimatService = talimatService;
        _mailService = mailService;
        _mailQueue = mailQueue;
        _configuration = configuration;
        _logger = logger;
    }

    private bool IsLoggedIn() =>
        !string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi"));

    private string KullaniciAdi() =>
        HttpContext.Session.GetString("KullaniciAdi") ?? "";

    private bool IsAdmin() =>
        HttpContext.Session.GetString("Rol") == "admin";

    private async Task<string?> GetCurrentUserEmailAsync()
    {
        var sessionEmail = HttpContext.Session.GetString("KullaniciEmail");
        if (!string.IsNullOrWhiteSpace(sessionEmail))
            return sessionEmail.Trim();

        var kullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        if (string.IsNullOrWhiteSpace(kullaniciAdi))
            return null;

        var connString = _configuration.GetConnectionString("MyConnection");
        if (string.IsNullOrWhiteSpace(connString))
            return null;

        try
        {
            await using var connection = new MySqlConnection(connString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT email FROM admin_kullanicilar WHERE kullanici_adi = @kullaniciAdi AND email IS NOT NULL AND TRIM(email) <> '' LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@kullaniciAdi", kullaniciAdi);

            var result = await command.ExecuteScalarAsync();
            if (result is string email && !string.IsNullOrWhiteSpace(email))
            {
                HttpContext.Session.SetString("KullaniciEmail", email.Trim());
                return email.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kullanıcı email sorgulanırken hata oluştu.");
        }

        return null;
    }

    // ================================================================
    // ANA SAYFA
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> Index(int? talimatId = null, int? firmaId = null)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            var sonTalimatlar = await _talimatService.GetSonTalimatlarAsync(20);
            OtTalimat? seciliTalimat = null;
            var talimatFirmalar = new List<OtFirma>();

            if (talimatId.HasValue && talimatId.Value > 0)
            {
                seciliTalimat = await _talimatService.GetTalimatDetayAsync(talimatId.Value);
                if (seciliTalimat != null)
                {
                    talimatFirmalar = await _talimatService.GetFirmalarByTalimatAsync(seciliTalimat.Id);
                }
            }

            ViewBag.Firmalar = talimatFirmalar;
            ViewBag.SonTalimatlar = sonTalimatlar;
            ViewBag.SeciliTalimat = seciliTalimat;
            ViewBag.KullaniciAdi = KullaniciAdi();
            ViewBag.QueueCount = _mailQueue.QueueCount;
            ViewBag.SeciliFirmaId = firmaId ?? 0;

            return View();
        }
        catch (Exception ex)
        {
            TempData["Hata"] = "Veri yüklenirken hata oluştu: " + ex.Message;
            return RedirectToAction("Index", "OdemeTalimat");
        }
    }

    // ================================================================
    // MAİL GÖNDER
    // ================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gonder([FromForm] MailGonderModel model)
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        try
        {
            // 1. Validasyon
            if (model.FirmaIdleri == null || model.FirmaIdleri.Count == 0)
            {
                TempData["Hata"] = "Lütfen en az bir firma seçin.";
                return RedirectToAction("Index", new { talimatId = model.TalimatId });
            }

            var talimat = await _talimatService.GetTalimatDetayAsync(model.TalimatId);
            if (talimat == null)
            {
                TempData["Hata"] = "Talimat bulunamadı.";
                return RedirectToAction("Index");
            }

            var talimatFirmalar = await _talimatService.GetFirmalarByTalimatAsync(model.TalimatId);
            var seciliFirmalar = talimatFirmalar.Where(f => model.FirmaIdleri.Contains(f.Id)).ToList();

            if (seciliFirmalar.Count == 0)
            {
                TempData["Hata"] = "Seçili firmalar bulunamadı.";
                return RedirectToAction("Index", new { talimatId = model.TalimatId });
            }

            // 2. Mail gönderimini kuyruğa al
            int gonderilen = 0;
            int basarisiz = 0;
            var hataListesi = new List<string>();

            foreach (var firma in seciliFirmalar)
            {
                if (string.IsNullOrEmpty(firma.Email))
                {
                    basarisiz++;
                    hataListesi.Add($"{firma.CariIsmi} - Email adresi yok");
                    continue;
                }

                try
                {
                    var firmaSatirlar = talimat.Satirlar
                        .Where(s => s.FirmaOdemeIsmi == firma.OdemeIsmi)
                        .ToList();

                    var mesajIcerik = await MailTemplateOlustur(talimat, firma, firmaSatirlar, model.Mesaj);

                    _mailQueue.Enqueue(new MailModel
                    {
                        To = firma.Email,
                        CC = model.CC,
                        Bcc = model.BCc,
                        Konu = model.Konu ?? $"Ödeme Talimatı - {talimat.TalimatNo}",
                        Body = mesajIcerik,
                        TalimatId = talimat.Id,
                        FirmaId = firma.Id,
                        GonderimZamani = model.GonderimZamani
                    });

                    gonderilen++;
                }
                catch (Exception ex)
                {
                    basarisiz++;
                    hataListesi.Add($"{firma.CariIsmi} - Hata: {ex.Message}");
                    _logger.LogError($"Mail gönderim hatası - Firma: {firma.CariIsmi}, Hata: {ex.Message}");
                }
            }

            // 3. Sonuç mesajı
            var sonucMesaj = $"✅ {gonderilen} firma mail kuyruğuna alındı.";
            if (basarisiz > 0)
            {
                sonucMesaj += $" ⚠️ {basarisiz} firmaya gönderilemedi.";
                if (hataListesi.Any())
                {
                    sonucMesaj += $"<br/><br/>Detaylar:<br/>- {string.Join("<br/>- ", hataListesi.Take(10))}";
                    if (hataListesi.Count > 10)
                        sonucMesaj += $"<br/>... ve {hataListesi.Count - 10} daha";
                }
            }
            sonucMesaj += $"<br/><br/>📊 Kuyrukta bekleyen: {_mailQueue.QueueCount} mail";

            TempData["Basarili"] = sonucMesaj;
            return RedirectToAction("Index", new { talimatId = model.TalimatId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Mail gönderim hatası: {ex.Message}");
            TempData["Hata"] = $"Mail gönderim hatası: {ex.Message}";
            return RedirectToAction("Index", new { talimatId = model.TalimatId });
        }
    }

    // ================================================================
    // MAIL ŞABLONU OLUŞTUR
    // ================================================================
    private async Task<string> MailTemplateOlustur(
        OtTalimat talimat,
        OtFirma firma,
        List<OtTalimatSatiri> satirlar,
        string ozelMesaj)
    {
        var tableHtml = @"<table>
            <thead>
                <tr>
                    <th>#</th>
                    <th>Fatura No</th>
                    <th style='text-align:right;'>Tutar (TL)</th>
                </tr>
            </thead>
            <tbody>";

        int sira = 1;
        foreach (var satir in satirlar)
        {
            tableHtml += $@"
                        <tr>
                            <td>{sira}</td>
                            <td>{satir.Aciklama}</td>
                            <td style='text-align:right;'>{satir.Tutar:N2}</td>
                        </tr>";
            sira++;
        }

        var toplam = satirlar.Sum(s => s.Tutar);

        tableHtml += $@"
                    </tbody>
                    <tfoot>
                        <tr class='total'>
                            <td colspan='2' style='text-align:right;'>TOPLAM</td>
                            <td style='text-align:right;'>{toplam:N2} TL</td>
                        </tr>
                    </tfoot>
                </table>";

        var ozelMesajHtml = string.IsNullOrEmpty(ozelMesaj) ? "" : $@"
                <div class='ozel-mesaj'>
                    <strong>📝 Özel Mesaj:</strong><br/>
                    {ozelMesaj.Replace("\n", "<br/>")}
                </div>";

        var data = new Dictionary<string, object>
        {
            ["FIRMA_ADI"] = firma.OdemeIsmi,
            ["TALIMAT_NO"] = talimat.TalimatNo,
            ["TARIH"] = DateTime.Now.ToString("dd.MM.yyyy"),
            ["BANKA"] = talimat.BankaSubeAdi,
            ["IBAN"] = talimat.BankaIBAN,
            ["TABLE_HTML"] = tableHtml,
            ["TOPLAM_ADET"] = satirlar.Count.ToString(),
            ["OZEL_MESAJ"] = ozelMesajHtml,
            ["GONDERIM_TARIHI"] = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
        };

        return await _mailService.RenderTemplateAsync("ODEME_TALIMATI", data);
    }

    // ================================================================
    // TEST MAİLİ GÖNDER
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> TestGonder([FromBody] MailGonderModel model)
    {
        if (!IsLoggedIn()) return Unauthorized();
        if (!IsAdmin()) return Forbid();

        try
        {
            var kullaniciEmail = await GetCurrentUserEmailAsync();
            if (string.IsNullOrWhiteSpace(kullaniciEmail))
                return Json(new { basarili = false, mesaj = "Email adresiniz tanımlı değil." });

            var talimat = await _talimatService.GetTalimatDetayAsync(model.TalimatId);
            if (talimat == null)
                return Json(new { basarili = false, mesaj = "Talimat bulunamadı." });

            var talimatFirmalar = await _talimatService.GetFirmalarByTalimatAsync(model.TalimatId);
            var seciliFirma = talimatFirmalar.FirstOrDefault(f => model.FirmaIdleri.Contains(f.Id));
            if (seciliFirma == null)
                return Json(new { basarili = false, mesaj = "Firma bulunamadı." });

            var firmaSatirlar = talimat.Satirlar
                .Where(s => s.FirmaOdemeIsmi == seciliFirma.OdemeIsmi)
                .ToList();

            var mesajIcerik = await MailTemplateOlustur(talimat, seciliFirma, firmaSatirlar, model.Mesaj);

            await _mailService.SendAsync(new MailModel
            {
                To = kullaniciEmail,
                Konu = "[TEST] " + model.Konu,
                Body = mesajIcerik,
                TalimatId = talimat.Id,
                FirmaId = seciliFirma.Id
            });

            return Json(new { basarili = true, mesaj = "Test maili gönderildi." });
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = ex.Message });
        }
    }

    // ================================================================
    // MAIL RAPORU
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> Rapor()
    {
        if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
        if (!IsAdmin()) return Forbid();

        // Log verilerini getir
        // Veritabanından mail log'larını çek
        ViewBag.KullaniciAdi = KullaniciAdi();
        return View();
    }

    // MailGonderController.cs - Preview metodu

[HttpPost]
public async Task<IActionResult> Preview([FromBody] PreviewModel model)
{
    if (!IsLoggedIn()) return Unauthorized();
    if (!IsAdmin()) return Forbid();

    try
    {
        var talimat = await _talimatService.GetTalimatDetayAsync(model.TalimatId);
        if (talimat == null)
            return Json(new { basarili = false, mesaj = "Talimat bulunamadı." });

        var talimatFirmalar = await _talimatService.GetFirmalarByTalimatAsync(model.TalimatId);
        var firma = talimatFirmalar.FirstOrDefault(f => f.Id == model.FirmaId);
        if (firma == null)
            return Json(new { basarili = false, mesaj = "Firma bulunamadı." });

        var firmaSatirlar = talimat.Satirlar
            .Where(s => s.FirmaOdemeIsmi == firma.OdemeIsmi)
            .ToList();

        var html = await MailTemplateOlustur(talimat, firma, firmaSatirlar, model.OzelMesaj);
        return Json(new { basarili = true, html = html });
    }
    catch (Exception ex)
    {
        return Json(new { basarili = false, mesaj = ex.Message });
    }
}


public class PreviewModel
{
    public int TalimatId { get; set; }
    public int FirmaId { get; set; }
    public string OzelMesaj { get; set; } = string.Empty;
}

}