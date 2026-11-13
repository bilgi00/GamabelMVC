using gamabelmvc.Models.StokSayim;
using gamabelmvc.Services.StokSayim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.STS;

[Authorize(Policy = "StsAdmin")]
public class StokSayimController(
    ISayimService sayimService,
    ISistemStokExcelService sistemStokExcelService) : Controller
{
    private string KullaniciAdi => User.Identity?.Name ?? "Bilinmeyen kullanıcı";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var sayimlar = await sayimService.GetSayimlarAsync(cancellationToken);
        return View(sayimlar);
    }

    [HttpGet]
    public IActionResult Yeni()
    {
        return View(new SayimOlusturViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(
        SayimOlusturViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var sayimId = await sayimService.SayimOlusturAsync(
            model,
            User.Identity?.Name ?? "Bilinmeyen kullanıcı",
            cancellationToken);
        TempData["SayimBasarili"] = "Sayım oturumu oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = sayimId });
    }

    [HttpGet]
    public async Task<IActionResult> Detay(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        var sayim = await sayimService.GetDetayAsync(id, cancellationToken);
        return sayim == null ? NotFound() : View(sayim);
    }

    [HttpGet]
    public IActionResult SistemStokSablonu()
    {
        return File(
            sistemStokExcelService.SablonOlustur(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "sistem-stogu-sablonu.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(SistemStokExcelService.MaksimumDosyaBoyutu + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = SistemStokExcelService.MaksimumDosyaBoyutu + 1024 * 1024)]
    public async Task<IActionResult> SistemStokOnizle(
        int id,
        IFormFile? dosya,
        CancellationToken cancellationToken)
    {
        if (dosya == null)
        {
            TempData["SayimHata"] = "Lütfen sistem stoğu Excel dosyası seçin.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        try
        {
            var preview = await sistemStokExcelService.OnizleAsync(
                id,
                dosya,
                KullaniciAdi,
                cancellationToken);
            return RedirectToAction(nameof(SistemStokOnizleme), new { id, token = preview.Token });
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
            return RedirectToAction(nameof(Detay), new { id });
        }
        catch (InvalidDataException)
        {
            TempData["SayimHata"] = "Excel dosyası okunamadı. Geçerli bir .xlsx dosyası seçin.";
            return RedirectToAction(nameof(Detay), new { id });
        }
    }

    [HttpGet]
    public IActionResult SistemStokOnizleme(int id, string token)
    {
        var preview = sistemStokExcelService.OnizlemeGetir(id, token, KullaniciAdi);
        if (preview == null)
        {
            TempData["SayimHata"] = "Önizleme bulunamadı veya süresi doldu. Excel dosyasını yeniden yükleyin.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        return View(preview);
    }

    [HttpGet]
    public IActionResult SistemStokHatalariIndir(int id, string token)
    {
        try
        {
            var file = sistemStokExcelService.HataliSatirlariExcelOlarakOlustur(id, token, KullaniciAdi);
            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"sistem-stogu-hatalari-sayim-{id}.xlsx");
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
            return RedirectToAction(nameof(Detay), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SistemStokKaydet(
        int id,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var rowCount = await sistemStokExcelService.OnizlemeyiKaydetAsync(
                id,
                token,
                KullaniciAdi,
                cancellationToken);
            TempData["SayimBasarili"] = $"{rowCount:N0} ürünün sistem stoğu sayıma kaydedildi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
            return RedirectToAction(nameof(SistemStokOnizleme), new { id, token });
        }

        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kapat(int id, CancellationToken cancellationToken)
    {
        try
        {
            await sayimService.SayimKapatAsync(
                id,
                User.Identity?.Name ?? "Bilinmeyen kullanıcı",
                cancellationToken);
            TempData["SayimBasarili"] = "Sayım kapatıldı.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
        }

        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YenidenAc(int id, CancellationToken cancellationToken)
    {
        try
        {
            await sayimService.SayimYenidenAcAsync(
                id,
                User.Identity?.Name ?? "Bilinmeyen kullanıcı",
                cancellationToken);
            TempData["SayimBasarili"] = "Kapalı sayım yeniden açıldı.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
        }

        return RedirectToAction(nameof(Detay), new { id });
    }
}
