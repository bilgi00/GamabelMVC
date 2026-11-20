using System.Data;
using System.Globalization;
using gamabelmvc.Models.StokSayim;
using gamabelmvc.Services.Raporlama;
using gamabelmvc.Services.StokSayim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.STS;

[Authorize(Policy = "StsAdmin")]
public class StokSayimController(
    ISayimService sayimService,
    ISistemStokExcelService sistemStokExcelService,
    IRaporServisi raporServisi) : Controller
{
    private static readonly RaporTanimi StokSayimRaporu = RaporKaydi.StokSayim;

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
    public async Task<IActionResult> SayimEkrani(int id, string? barkod, CancellationToken cancellationToken)
    {
        var model = await sayimService.GetSayimEkraniAsync(id, barkod, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SayimEkle(
        int id,
        string barkod,
        decimal? miktar,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !miktar.HasValue)
        {
            TempData["SayimHata"] = "Geçerli bir sayım miktarı girin.";
            return RedirectToAction(nameof(SayimEkrani), new { id, barkod });
        }

        try
        {
            var urunAdi = await sayimService.SayimEkleAsync(
                id,
                barkod,
                miktar.Value,
                KullaniciAdi,
                cancellationToken);
            TempData["SayimBasarili"] = $"{urunAdi} için {miktar.Value:N3} sayım miktarı kaydedildi.";
            return RedirectToAction(nameof(SayimEkrani), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["SayimHata"] = ex.Message;
            return RedirectToAction(nameof(SayimEkrani), new { id, barkod });
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Rapor(int id, string yon = "yatay", CancellationToken cancellationToken = default)
    {
        if (!RaporYon.TryCoz(yon, out var yonu))
            return BadRequest("Geçersiz yön. Geçerli değer: yatay");
        if (!StokSayimRaporu.YonDestekliyor(yonu))
            return BadRequest("Stok sayım raporu yalnızca yatay A4 olarak hazırlanmıştır.");

        var model = await sayimService.GetSayimRaporuAsync(id, cancellationToken);
        if (model == null)
            return NotFound();

        var (veri, parametreler) = SayimRaporVerisiHazirla(model);
        var webRapor = raporServisi.Onizleme(StokSayimRaporu, yonu, veri, parametreler);
        ViewBag.Yon = yonu;
        ViewBag.DesteklenenYonler = StokSayimRaporu.Yonler();
        ViewBag.RaporQuery = $"id={id}";
        ViewBag.PdfUrl = Url.Action(nameof(RaporPdf), new { id, yon = yonu.ToString().ToLowerInvariant(), indir = true });
        return View("~/Views/Shared/Rapor.cshtml", webRapor);
    }

    [HttpGet]
    public async Task<IActionResult> RaporPdf(
        int id,
        string yon = "yatay",
        bool indir = false,
        CancellationToken cancellationToken = default)
    {
        if (!RaporYon.TryCoz(yon, out var yonu))
            return BadRequest("Geçersiz yön. Geçerli değer: yatay");
        if (!StokSayimRaporu.YonDestekliyor(yonu))
            return BadRequest("Stok sayım raporu yalnızca yatay A4 olarak hazırlanmıştır.");

        var model = await sayimService.GetSayimRaporuAsync(id, cancellationToken);
        if (model == null)
            return NotFound();

        var (veri, parametreler) = SayimRaporVerisiHazirla(model);
        var cikti = raporServisi.Pdf(StokSayimRaporu, yonu, veri, parametreler);
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var dosyaAdi = $"stok-sayim-{id}-{DateTime.Now:yyyyMMdd}.pdf";
        return indir
            ? File(cikti.Icerik, "application/pdf", dosyaAdi)
            : File(cikti.Icerik, "application/pdf");
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

    private (DataSet Veri, Dictionary<string, object?> Parametreler) SayimRaporVerisiHazirla(
        SayimRaporViewModel model)
    {
        var satirlar = new DataTable("Satirlar");
        satirlar.Columns.Add("SiraNo", typeof(int));
        satirlar.Columns.Add("StokKodu", typeof(string));
        satirlar.Columns.Add("Barkod", typeof(string));
        satirlar.Columns.Add("UrunAdi", typeof(string));
        satirlar.Columns.Add("GirenMiktar", typeof(decimal));
        satirlar.Columns.Add("CikanMiktar", typeof(decimal));
        satirlar.Columns.Add("KalanMiktar", typeof(decimal));
        satirlar.Columns.Add("SayimMiktari", typeof(decimal));
        satirlar.Columns.Add("Fark", typeof(decimal));
        satirlar.Columns.Add("Birim", typeof(string));

        for (var index = 0; index < model.Satirlar.Count; index++)
        {
            var satir = model.Satirlar[index];
            satirlar.Rows.Add(
                index + 1,
                satir.StokKodu ?? string.Empty,
                satir.Barkod ?? string.Empty,
                satir.UrunAdi,
                (object?)satir.GirenMiktar ?? DBNull.Value,
                (object?)satir.CikanMiktar ?? DBNull.Value,
                satir.KalanMiktar,
                satir.SayimMiktari,
                satir.Fark,
                satir.Birim ?? string.Empty);
        }

        var veri = new DataSet("Data");
        veri.Tables.Add(satirlar);
        var parametreler = new Dictionary<string, object?>
        {
            ["BaslikAdi"] = "STOK SAYIM RAPORU",
            ["SayimAdi"] = model.SayimAdi,
            ["SayimDurumu"] = model.Durum,
            ["SayimNo"] = model.SayimId.ToString(CultureInfo.InvariantCulture),
            ["Tarih"] = DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")),
            ["OlusturanAd"] = KullaniciAdi,
            ["UrunSayisi"] = model.Satirlar.Count,
            ["SayilanUrunSayisi"] = model.Satirlar.Count(satir => satir.SayildiMi)
        };
        return (veri, parametreler);
    }
}
