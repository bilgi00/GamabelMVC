# FastReport A4 Rapor Standardı (GamabelMVC) — v3

> Yapay zekaya **"… sayfasındaki verileri raporla"** denildiğinde uygulanacak **tek ve bağlayıcı** rapor standardıdır.
> Hedef: Her rapor **A4**, **dikey veya yatay**, **FastReport önizleme + PDF** çıktılı olsun; tasarım **`*.frx`** dosyasından okunsun.

> **Proje uyarlama notu:** GamabelMVC'de FastReport Web ve PDF paketleri ile `AddFastReport()` / `UseFastReport()` zaten kullanılmaktadır. Ödeme talimatı, mesai ve puantaj raporları ayrı controller, servis/veri hazırlama kodu ve `.frx` dosyalarıyla çalışır. Bu standart yeni raporlarda ortaklaşmayı hedefler; mevcut route'ları, yetki kurallarını, veri erişimini veya çalışan şablonları toplu ve otomatik olarak değiştirme talimatı değildir. Aşağıdaki C# ve `.frx` örnekleri uyarlanacak iskelettir; gerçek projede derlenip **FastReport Designer'da ve PDF çıktısında** doğrulanmalıdır (Bölüm 15 ve 16).

---

## 0. v2 Değişiklik Özeti

| Kod | Tür | Konu | Bölüm |
|---|---|---|---|
| H1 | Hata | PDF export paketi eksikti | 4 |
| H2 | Hata | Mevcut `Rapor.cshtml` / `RaporPdf2` / `OdemeTalimat.frx` ile çakışma | 3 |
| H3 | Hata | Yetkilendirme yoktu | 6 |
| H4 | Hata | Sabit tablo adına bağlı veri bağlama | 5 |
| H5 | Hata | Test edilmemiş şablonlar, fazla iddialı ifade, eksik yatay şablon | 1, 8, 16 |
| H6 | Hata | Tablo başlığı `PageHeaderBand`'deydi (sıra sorunu) | 8 |
| H7 | Hata | `[TotalPages#]` için `DoublePass` yoktu | 8 |
| H8 | Hata | `GrowToBottom` eksik, band `Top` değerleri elle yazılmıştı | 8 |
| H9 | Hata | PDF önbellek başlığı ve nesne temizliği yoktu | 5, 6 |
| H10 | Hata | Yön kuralı keyfiydi, geçersiz `yon` sessiz geçiliyordu; `RaporVerisiGetir` tanımsızdı | 2, 6 |
| Ö1 | Öneri | Tam yatay `.frx` + kolon genişliği kuralı | 2, 8 |
| Ö2 | Öneri | `RaporTanimi` + `IRaporServisi` (DI) | 5 |
| Ö3 | Öneri | Türkçe karakter, font, sayı/tarih biçimi | 11 |
| Ö4 | Öneri | Otomatik doğrulama testleri | 15 |
| Ö5 | Öneri | Kurumsal ortak öğeler (logo, filigran, imza, alt bilgi) | 10 |
| Ö6 | Öneri | Şablon kütüphanesi (5 tip) | 9 |
| Ö7 | Öneri | Çıktı denetim kaydı, belge no, QR | 10 |
| Ö8 | Öneri | Excel dışa aktarma ve PDF kısıtları | 12 |
| Ö9 | Öneri | Büyük veri: arka plan üretim + önbellek | 13 |
| Ö10 | Öneri | Şablon sürümleme + önizlemede yön seçici | 7, 14 |

### v3 — GamabelMVC uyarlamaları

- Projenin `gamabelmvc` namespace'i ve mevcut rapor veri kaynakları esas alınır; örnek `AppDbContext` veya farklı namespace varsayılmaz.
- A4 için 10 mm kenar boşluğu bir varsayılandır. Şablon genişliği sayfanın değil, kullanılan kenar boşluklarından sonraki alanın ölçüsüne göre hesaplanır.
- Mevcut raporlar farklı kenar boşluğu, `.frx` adı, route, yetki politikası ve band düzeni kullanabilir. Geçiş rapor bazında yapılır; eski bağlantıların davranışı korunur.
- Denetim kaydı, QR, logo, yön seçici ve şablon ikilisi raporun iş gereksinimi ve mevcut yetki/veri altyapısı uygunsa eklenir; her rapor için zorunlu sayılmaz.
- Testler mevcut test altyapısı keşfedildikten sonra eklenir. Statik kontroller FastReport Designer/PDF görsel doğrulamasının yerine geçmez.

---

## 1. Yapay Zeka İçin Altın Kurallar

1. Yeni rapor tasarımı **`.frx` dosyasında** yapılır; yeni C# ile çizilmiş sayfa/tablo yolu eklenmez. Mevcut kodla üretilen rapor yardımcıları varsa kullanım yerleri doğrulanmadan silinmez veya topluca dönüştürülmez.
2. Sayfa boyutu **her zaman A4**'tür.
3. Yeni raporda kolon/veri yoğunluğu her iki düzeni anlamlı kılıyorsa `*.Dikey.frx` ve `*.Yatay.frx` üretilir. Tek yön yeterliyse veya mevcut raporun geriye dönük uyumluluğu iki şablona geçişi engelliyorsa yalnızca uygun yön kullanılır; eksik yön sessizce uydurulmaz.
4. Önizleme ve PDF **aynı `.frx` dosyasından** üretilir. Yerleşim aynıdır; ancak HTML önizleme ile PDF arasında font ve satır kaydırma düzeyinde küçük farklar olabilir. **Nihai doğruluk kaynağı PDF'tir.**
5. Proje **FastReport.OpenSource.Web 2026.2.3** kullanır. Şu eski API'ler **KULLANILMAZ:** `GetHtml()`, `ReportFile`, `ShowToolbar`, `WebReportGlobals.Scripts()`, `WebReportGlobals.Styles()`, `Server.MapPath()`, `PDFExport`.
6. Güncel kullanım: `new WebReport()` → `Report.Load(...)` → `@await Model.Render()` ve `app.UseFastReport()`.
7. Mevcut dosyalar (`Program.cs` pipeline sırası, `wwwroot/_content/FastReport.Web/*`) **bozulmaz / yeniden oluşturulmaz**.
8. Proje `FineReport` değil **FastReport** kullanır. `.cpt` değil `.frx` üretilir.
9. **Rapor üretmeden önce mevcut `Rapor*` action'ları, view'ları ve `.frx` dosyaları taranır;** aynı rapor için ikinci bir yol oluşturulmaz (Bölüm 3).
10. **Her rapor action'ı mevcut modül yetkisini ve kayıt/birim kapsamını korur** (Bölüm 6). Yeni bir genel policy adı eklemek veya mevcut policy'yi gevşetmek yerine o modülün doğrulanmış yetkilendirme kuralı kullanılır.
11. Şablon ve kod, gerçek projede **derlenmeden "tamamlandı" denmez**; derleme yapılamadıysa kullanıcıya açıkça söylenir.

---

## 2. A4 Ölçüleri ve Yön Kararı

### 2.1 Ölçü tablosu

| Özellik | Dikey (Portrait) | Yatay (Landscape) |
|---|---|---|
| `Landscape` | `false` | `true` |
| `PaperWidth` (mm) | `210` | `297` |
| `PaperHeight` (mm) | `297` | `210` |
| Örnek kenar boşlukları (mm) | Sol/Sağ/Üst/Alt: `10` | Sol/Sağ/Üst/Alt: `10` |
| Örnek kullanılabilir genişlik (mm) | `190` | `277` |
| **Örnek band/obje genişliği (px)** | **`718.2`** | **`1047.06`** |
| PDF sayfa boyutu (pt) | ≈ 595 × 842 | ≈ 842 × 595 |
| Dosya adı | `{Rapor}.Dikey.frx` | `{Rapor}.Yatay.frx` |

> `1 mm ≈ 3.78 px`. Bu px değerleri yalnızca 10 mm sol/sağ kenar boşluğu için örnektir; zorunlu sabit değildir. Kullanılabilir genişliği `(sayfa eni - sol kenar boşluğu - sağ kenar boşluğu) × 3.78` ile hesaplayın. Örneğin yatay A4'te 6 mm kenar boşluğu 1077.3 px kullanılabilir genişlik verir. Mevcut puantaj şablonu bu ölçüyü kullanır. Raporun imza/yazdırma gereksinimi başka boşluk istiyorsa gerçek PDF çıktısını doğrulayarak tanımlayın.

### 2.2 Kolon genişliği rehberi (px)

| Kolon tipi | Önerilen genişlik |
|---|---|
| Sıra no | 40 – 57 |
| Tarih | 70 – 80 |
| Tutar / sayı | 90 – 130 |
| Kısa metin (kod, durum) | 80 – 120 |
| Orta metin (ad, birim) | 150 – 200 |
| Uzun metin (açıklama) | ≥ 250 (`CanGrow` + `WordWrap`) |

### 2.3 Yön kararı (kolon sayısı değil, toplam genişlik)

| Koşul | Yön |
|---|---|
| Kolon genişlikleri toplamı ≤ dikey şablonun kullanılabilir genişliği | **Dikey** |
| Dikey kullanılabilir genişlik < toplam ≤ yatay şablonun kullanılabilir genişliği | **Yatay** |
| Toplam > yatay kullanılabilir genişlik | Kolonları azalt / böl; gerekirse ana-detay veya ek liste şablonu kullan (Bölüm 9) |
| Kullanıcı yön belirttiyse | Kullanıcının seçimi geçerlidir (taşma varsa uyar) |

**`yon` parametresi sıkı doğrulanır:** yalnızca `dikey` veya `yatay` kabul edilir. Başka değerde **HTTP 400** döner (sessizce dikeye düşmez).

---

## 3. Klasör, İsimlendirme ve Mevcut Projeyle Geçiş

```
Reports/
└── {ModulAdi}/
    ├── {RaporAdi}.Dikey.frx
    └── {RaporAdi}.Yatay.frx

Services/Raporlama/                    ← ortak C# sınıfları (namespace gamabelmvc.Services.Raporlama)
├── RaporYonu.cs
├── RaporTanimi.cs                     ← tanım + kayıt
├── IRaporServisi.cs
├── RaporServisi.cs
└── IRaporCiktiLogServisi.cs

Views/Shared/Rapor.cshtml              ← ortak önizleme view'i
Controllers/{Alan}/{Modul}Controller.cs
```

| Öğe | Kural | Örnek |
|---|---|---|
| Yeni rapor dosyası | `{RaporAdi}.{Yon}.frx` | `OdemeTalimat.Dikey.frx` |
| Örnek önizleme route'u | `/{Modul}/Rapor/{id}?yon=dikey\|yatay` | `/OdemeTalimat/Rapor/54?yon=yatay` |
| Örnek PDF route'u | `/{Modul}/RaporPdf/{id}?yon=dikey\|yatay&indir=true\|false` | `/OdemeTalimat/RaporPdf/54` |
| Varsayılan yön | `dikey` | — |
| Veri kaynağı | `.frx` `ReferenceName` ile FastReport'a kaydedilen DataTable/DataSet adı birebir eşleşir | `OdemeTalimat` ↔ `DataTable("OdemeTalimat")` |

Bu route ve adlandırmalar **yeni raporlar için hedef örnektir**, GamabelMVC'nin tüm mevcut endpoint'leri değildir. Birden fazla ilişkili tablo gereken raporda `DataSet` kullanılabilir; mevcut tek tablo raporlarında `DataTable` doğrudan kaydedilebilir veya mevcut FRX adını koruyan ince bir adaptör kullanılabilir. Her raporu `DataSet("Data")` biçimine zorlamayın.

### 3.1 Mevcut rapor envanteri ve uyumlu geçiş (H2)

| Mevcut öğe | Yeni standartta | Yapılacak |
|---|---|---|
| `Reports/OdemeTalimat/OdemeTalimat.frx` | Gerekirse yönlü şablon çifti | Önce çıktıyı ve bankaya verilen belge düzenini karşılaştır. Eski dosyayı hemen yeniden adlandırma; geçişte eski ad için uyumluluk sağla. |
| `Reports/Mesai.frx`, `Reports/Mesai/*.frx` | Rapor bazında yönlü şablon(lar) | `MesaiController`'ın `DataTable("Mesai")` bağlama biçimini ve birim/personel bazlı çıktıları koru. |
| `Reports/Puantaj/Puantaj.frx` | Mevcut yatay rapor veya yönlü adlandırma | 6 mm kenar boşluğu ve 1077.3 px kullanılabilir alanı koruyup PDF çıktısında doğrula; 10 mm varsayılanını zorla uygulama. |
| `RaporPdf2/{id}` action | Var olan davranış korunur | Şu an ödeme talimatı `RaporPdf2` önizlemeye yönlendiriyor. PDF indirmeye çevirmeyin; route'u ancak çağrı yerleri ve iş davranışı doğrulandıktan sonra değiştirin. Kalıcı yönlendirme kullanıcı/bookmark bağlantılarını kalıcılaştıracağından varsayılan geçiş yöntemi değildir. |
| `Views/PRS/OdemeTalimat/Rapor.cshtml` ve modüle özel preview view'ları | İhtiyaç varsa ortak view | Ortak view'e geçişte controller/view davranışını doğrulayın; kullanılmayan dosya olduğu teyit edilmeden silmeyin. |
| `Detay.cshtml` ve rapor açan diğer butonlar | Uyumlu bağlantılar | Her bağlantının mevcut önizleme/PDF davranışını ve yetkisini koruyun. |

> **Kural:** Yeni rapor eklemek mevcut raporları toplu yeniden adlandırmayı gerektirmez. Mevcut raporu taşımak gerekiyorsa önce route, buton, yetki ve şablon çağrılarını envanterleyin; geçişi uyumluluğu koruyarak ve rapor bazında yapın.

---

## 4. Paketler ve `Program.cs` (H1)

`FastReport.OpenSource.Web` zaten çekirdek paketi getirir. PDF için **ayrı paket** gerekir:

```xml
<ItemGroup>
  <!-- Sürüm, projedeki FastReport.OpenSource.Web ile AYNI olmalı -->
  <PackageReference Include="FastReport.OpenSource.Export.PdfSimple" Version="2026.2.3" />
</ItemGroup>

<ItemGroup>
  <None Update="Reports\**\*.frx" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

> Paket adı veya sürüm NuGet'te farklıysa (`dotnet list package`) projedeki Web paketinin sürümüne eşitle ve `using FastReport.Export.PdfSimple;` ad alanının çözümlendiğini derleyerek doğrula.

`Program.cs` (mevcut sıra **korunur**, yalnızca eksikler eklenir; FastReport kaydı projede zaten varsa tekrarlanmaz):

```csharp
builder.Services.AddFastReport(); // Yalnızca mevcut değilse; projede zaten kayıtlı.
// Yalnızca ortak rapor servisi/önbellek gerçekten eklenecekse ilgili DI kaydını yapın.
builder.Services.AddScoped<IRaporServisi, RaporServisi>();
// IRaporCiktiLogServisi isteğe bağlıdır; yalnızca iş gereksinimi ve kalıcı depolama
// kararı varsa ekleyin. Mevcut authorization policy kayıtlarını değiştirmeyin.

// ...
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastReport();      // AddFastReport() sonrası mutlaka pipeline'da olmalı
```

---

## 5. Ortak Raporlama Katmanı (Ö2, H4, H9)

### 5.1 Tanım ve kayıt

```csharp
namespace gamabelmvc.Services.Raporlama;

public enum RaporYonu { Dikey, Yatay }

public static class RaporYon
{
    public static bool TryCoz(string? yon, out RaporYonu sonuc)
    {
        switch (yon?.Trim().ToLowerInvariant())
        {
            case "dikey": sonuc = RaporYonu.Dikey; return true;
            case "yatay": sonuc = RaporYonu.Yatay; return true;
            default:      sonuc = default;         return false;
        }
    }
}

/// <summary>Bir raporun tek yerdeki tanımı.</summary>
public sealed record RaporTanimi(
    string Modul,               // Reports/{Modul}
    string Ad,                  // {Ad}.Dikey.frx
    string[] Tablolar,          // Beklenen DataTable adları
    string YetkiPolitikasi,     // Modülün mevcut Authorization policy adı
    string? BelgeKodu = null,   // Yalnızca belge numarası kullanılıyorsa
    RaporYonu VarsayilanYon = RaporYonu.Dikey);

public static class RaporKaydi
{
    // Bu kayıt, Bölüm 8'deki örnek FRX'in Data.Satirlar kaynağıyla eşleşir.
    public static readonly RaporTanimi OdemeTalimat =
        new("OdemeTalimat", "OdemeTalimat", new[] { "Satirlar" }, "PrsAdmin", "OT");

    // Yeni rapor = buraya tek satır
}
```

### 5.2 Servis arayüzü

```csharp
using System.Data;
using FastReport.Web;

namespace gamabelmvc.Services.Raporlama;

public sealed record RaporCikti(byte[] Icerik, string SablonSurum);

public interface IRaporServisi
{
    WebReport Onizleme(RaporTanimi tanim, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm);
    RaporCikti Pdf(RaporTanimi tanim, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm);
}
```

> Bu iskelet çok tablolu raporlar için `DataSet` imzasını gösterir. GamabelMVC'deki gibi tek `DataTable` ile çalışan raporlarda tabloyu `.frx`'in mevcut `ReferenceName` adıyla doğrudan kaydedin veya yalnızca ortak servisin içinde ince bir adaptörle `DataSet`'e sarın; controller'ları ve çalışan FRX kaynak adlarını ortak imzaya uydurmak için yeniden yazmayın.

### 5.3 Servis uygulaması

```csharp
using System.Data;
using FastReport;
using FastReport.Data;
using FastReport.Export.PdfSimple;
using FastReport.Web;

namespace gamabelmvc.Services.Raporlama;

public sealed class RaporServisi : IRaporServisi
{
    private readonly IWebHostEnvironment _env;
    public RaporServisi(IWebHostEnvironment env) => _env = env;

    public WebReport Onizleme(RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        var web = new WebReport();
        Hazirla(web.Report, t, yon, veri, prm);

        web.Width = "100%";
        web.Height = "calc(100vh - 24px)";   // üstte 24px yön seçici çubuğu için yer bırakır
        web.Inline = false;

        web.Toolbar.Show = true;
        web.Toolbar.ShowPrint = true;
        web.Toolbar.Exports.Show = true;
        web.Toolbar.Exports.ShowPreparedReport = true;
        return web;
    }

    public RaporCikti Pdf(RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        using var report = new Report();
        Hazirla(report, t, yon, veri, prm);
        report.Prepare();

        using var ms = new MemoryStream();
        new PDFSimpleExport().Export(report, ms);
        return new RaporCikti(ms.ToArray(), report.ReportInfo.Version ?? "0");
    }

    private void Hazirla(Report report, RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        var frx = Path.Combine(_env.ContentRootPath, "Reports", t.Modul, $"{t.Ad}.{yon}.frx");
        if (!File.Exists(frx))
            throw new FileNotFoundException($"Rapor dosyası bulunamadı: {frx}");

        report.Load(frx);

        // H4: Tablo adı sabit yazılmaz; beklenen tabloların varlığı denetlenir, hepsi etkinleştirilir.
        foreach (var tablo in t.Tablolar)
            if (!veri.Tables.Contains(tablo))
                throw new InvalidOperationException(
                    $"'{t.Ad}' raporu için '{tablo}' tablosu DataSet içinde yok. Mevcut: " +
                    string.Join(", ", veri.Tables.Cast<DataTable>().Select(x => x.TableName)));

        report.RegisterData(veri, "Data");
        foreach (DataSourceBase kaynak in report.Dictionary.DataSources)
            kaynak.Enabled = true;

        foreach (var kv in prm)
            report.SetParameterValue(kv.Key, kv.Value);

        report.SetParameterValue("SablonSurum", report.ReportInfo.Version ?? "0");

        // Ortak kurumsal öğeler (Bölüm 10)
        if (report.FindObject("PicLogo") is PictureObject logo)
            logo.ImageLocation = Path.Combine(_env.WebRootPath, "img", "logo.png");

        if (prm.TryGetValue("Filigran", out var f) && f is string filigran
            && !string.IsNullOrWhiteSpace(filigran)
            && report.Pages.Count > 0 && report.Pages[0] is ReportPage sayfa)
        {
            sayfa.Watermark.Enabled = true;
            sayfa.Watermark.Text = filigran;     // örn. "TASLAK"
        }
    }
}
```

> PDF üretiminde `Report` ve export nesnelerini `using` ile kapatın. Web önizlemesinde FastReport `WebReport` raporunu ilk HTML yanıtından sonra gelen ayrı isteklerde kullanabilir; `web.Report` nesnesini ilk controller response'u biter bitmez `Response.RegisterForDispose(...)` ile kapatmayın. Nesne ömrünü FastReport.Web'in preview istekleriyle uyumlu yönetin ve bellek kullanımı/çoklu istek davranışını uygulama sürümünde doğrulayın (H9).

> **Boş veri durumu:** Boş `DataBand`, bazı FRX düzenlerinde sıfır hazırlanmış sayfa bırakabilir. `WebReport` böyle bir raporu oluşturmaya çalışırken `PreparedPages.GetPageSize(0)` kaynaklı `ArgumentOutOfRangeException` verebilir. Boş sonucu açıkça tasarlayın: örneğin aynı kolonları taşıyan tek bir “Kayıt bulunamadı” satırı ekleyin veya rapor içinde boş durum bandı kullanın. Başlıkta gerçek kayıt sayısını gösterin; placeholder satırını gerçek kayıt olarak saymayın. Önizleme ve PDF'de hem boş hem dolu veriyle sınayın.

---

## 6. Controller Şablonu (H3, H9, H10)

> Aşağıdaki örnek akış şablonudur; GamabelMVC'ye doğrudan kopyalanacak controller değildir. Projede ödeme talimatı için `PrsAdmin`, mesai için `PrsMenuMesai`, puantaj için `PrsMenuPuantaj` politikaları kullanılır. İlgili modülün gerçek policy'sini ve mevcut servis/veri hazırlama yolunu koruyun. Kayıt kapsamı (ör. birim/personel) rol kontrolünden ayrı doğrulanmalıdır. Bu örnekteki `AppDbContext` veya EF Core erişimi zorunlu değildir ve bu projede doğrulanmış varsayım olarak kullanılmamalıdır.

```csharp
[Authorize(Policy = "ModulunMevcutPolicyAdi")]
public class OdemeTalimatController : Controller
{
    private readonly IRaporServisi _rapor;
    private readonly OdemeTalimatService _talimatService; // Var olan modül servisi örneği

    private static readonly RaporTanimi Tanim = RaporKaydi.OdemeTalimat;

    // ÖNİZLEME (+ yazdırma + dışa aktarma menüsü)
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Rapor(int id, string yon = "dikey")
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");

        // Mevcut servis ve modülün kayıt/birim yetkisi kontrolü kullanılmalıdır.
        var kayit = await YetkiliKayitGetirAsync(id);
        if (kayit is null) return NotFound();     // id tahminini önlemek için 404 (yetkisizlik de 404)

        var (ds, prm) = RaporVerisiHazirla(kayit);
        // Çıktı logu yalnızca bu modül için gereksinim ve depolama kararı varsa eklenir.

        var web = _rapor.Onizleme(Tanim, y, ds, prm);
        // web.Report'u bu ilk response tamamlanınca dispose etmeyin;
        // FastReport.Web sonraki isteklerde de rapora erişebilir.
        ViewBag.Yon = y;
        ViewBag.KayitId = id;
        return View("~/Views/Shared/Rapor.cshtml", web);
    }

    // PDF İNDİR / TARAYICIDA AÇ
    [HttpGet]
    public async Task<IActionResult> RaporPdf(int id, string yon = "dikey", bool indir = false)
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");

        var kayit = await YetkiliKayitGetirAsync(id);
        if (kayit is null) return NotFound();

        var (ds, prm) = RaporVerisiHazirla(kayit);
        var cikti = _rapor.Pdf(Tanim, y, ds, prm);

        // H9: Hassas rapor yanıtı istemci/proxy önbelleğine alınmaz.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        var ad = $"{Tanim.Ad}_{id}_{DateTime.Now:yyyyMMdd}.pdf";
        return indir ? File(cikti.Icerik, "application/pdf", ad)
                     : File(cikti.Icerik, "application/pdf");
    }

    // Eski route'un mevcut davranışını koruyun; kaldırma/değiştirme öncesi çağrı yerlerini tarayın.
    [HttpGet]
    public IActionResult RaporPdf2(int id) => RedirectToAction(nameof(Rapor), new { id });

    // --- Yardımcılar ---

    /// Kayıt yoksa veya kullanıcının yetkisi yoksa null döner.
    private async Task<OtTalimat?> YetkiliKayitGetirAsync(int id)
    {
        var kayit = await _talimatService.GetTalimatDetayAsync(id);
        if (kayit is null) return null;

        // Burada mevcut modülün rol/birim/kayıt kapsamı kuralını uygulayın.
        return ModulKayitYetkisiVar(kayit) ? kayit : null;
    }

    /// Tablo ve kolon adlarını .frx ile birebir eşleştirin; mevcut raporlar DataTable kullanabilir.
    private (DataSet, Dictionary<string, object?>) RaporVerisiHazirla(OtTalimat kayit)
    {
        var dt = new DataTable("Satirlar");
        dt.Columns.Add("SiraNo", typeof(int));
        dt.Columns.Add("Aciklama", typeof(string));
        dt.Columns.Add("Tutar", typeof(decimal));

        var sira = 1;
        foreach (var s in kayit.Satirlar)
            dt.Rows.Add(sira++, s.Aciklama, s.Tutar);

        var ds = new DataSet("Data");
        ds.Tables.Add(dt);

        var prm = new Dictionary<string, object?>
        {
            ["BaslikAdi"]   = "ÖDEME TALİMATI",
            ["OlusturanAd"] = User.Identity?.Name,
            ["Tarih"]       = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            ["Filigran"]    = null // Yalnızca gerçek onay durumu varsa buradan belirleyin.
        };
        return (ds, prm);
    }
}
```

> Örnek içindeki `ModulKayitYetkisiVar` ve gerçek servis/model alanları yer tutucudur; projeye eklenmeden önce ilgili controller ve servisle eşleştirilir. Mevcut controller zaten doğru policy ve kapsam denetimi yapıyorsa onu koruyun, ikinci bir authorization katmanı oluşturmayın.

---

## 7. Ortak View ve Yön Seçici (Ö10)

`Views/Shared/Rapor.cshtml` (genel layout'tan bağımsız; üstte 24 px'lik çubuk `Height = calc(100vh - 24px)` ile uyumludur):

```cshtml
@model FastReport.Web.WebReport
@{
    Layout = null;
    var yon = (gamabelmvc.Services.Raporlama.RaporYonu)ViewBag.Yon;
    var id = (int)ViewBag.KayitId;
}
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Rapor Önizleme</title>
    <style>
        html, body { margin: 0; padding: 0; height: 100%; font-family: Arial, sans-serif; }
        .rapor-cubuk { height: 24px; line-height: 24px; padding: 0 8px; background: #f1f3f5; font-size: 12px; }
        .rapor-cubuk a { margin-right: 10px; text-decoration: none; color: #0b5ed7; }
        .rapor-cubuk a.aktif { font-weight: bold; color: #000; pointer-events: none; }
    </style>
</head>
<body>
    <div class="rapor-cubuk">
        Yön:
        <a class="@(yon == gamabelmvc.Services.Raporlama.RaporYonu.Dikey ? "aktif" : "")" href="?yon=dikey">Dikey</a>
        <a class="@(yon == gamabelmvc.Services.Raporlama.RaporYonu.Yatay ? "aktif" : "")" href="?yon=yatay">Yatay</a>
        <a href="@Url.Action("RaporPdf", new { id, yon = yon.ToString().ToLowerInvariant(), indir = true })">PDF indir</a>
    </div>
    @await Model.Render()
</body>
</html>
```

Detay/Liste sayfasındaki butonlar:

```cshtml
<a class="btn btn-outline-primary" target="_blank" asp-action="Rapor" asp-route-id="@Model.Id" asp-route-yon="dikey">Önizleme (Dikey)</a>
<a class="btn btn-outline-primary" target="_blank" asp-action="Rapor" asp-route-id="@Model.Id" asp-route-yon="yatay">Önizleme (Yatay)</a>
<a class="btn btn-danger"          target="_blank" asp-action="RaporPdf" asp-route-id="@Model.Id" asp-route-yon="dikey" asp-route-indir="true">PDF</a>
```

> Kenar boşluğu değiştirmek tek başına yeni şablon gerektirmez; kullanılabilir alan ve içindeki kolon yerleşimi yeniden hesaplanıp Designer/PDF çıktısında doğrulanmalıdır. Gerçekten farklı bir yerleşim gerekiyorsa ayrı şablon tercih edilir.

---

## 8. `.frx` Kuralları ve Tam Şablonlar (H5–H8, Ö1)

### 8.1 Şablon kuralları

| Kural | Neden |
|---|---|
| Toplam sayfa sayısı gerekiyorsa `DoublePass="true"` kullan ve PDF'de doğrula | `[TotalPages#]` gibi toplam sayfa alanlarının değerlendirilmesi (H7) |
| Veri bandına bağlı kolon başlığında `DataHeaderBand` ve gerekiyorsa `RepeatOnEveryPage="true"` tercih et | Başlık veri bandıyla birlikte tekrar edebilir; mevcut `PageHeaderBand` tasarımlarını yalnızca bu nedenle zorunlu olarak taşıma (H6) |
| `PageHeaderBand` kurumsal veya her sayfada tekrarlanması gereken içerik için kullanılabilir | GamabelMVC'nin mevcut Mesai/Puantaj raporlarında kolon başlıkları bu band içindedir; yerleşimi gerçek raporda doğrula |
| Band `Top` değerlerini elle hesaplayıp yazma; Designer'ın oluşturduğu FRX koordinatlarını koru | FastReport FRX mevcut şablonlarda band `Top` koordinatlarını serileştirebilir; bu değerlerin varlığı tek başına hata değildir (H8) |
| Uzayan metin içeren satırda `CanGrow`, `WordWrap` ve gerekirse `GrowToBottom` davranışını örnek uzun metinlerle test et | Her hücreye koşulsuz `GrowToBottom` eklemek yerine gerçek satır yüksekliği/kenarlık davranışını doğrula (H8) |
| Sayı/tarih biçimi **açık yazılır** (`Format.UseLocale="false"`) | Sunucu kültüründen bağımsız (Bölüm 11) |
| `ReportInfo.Version` ve `ReportInfo.Description` güncellenir | Şablon sürümleme (Bölüm 14) |
| `Left + Width`, kenar boşluklarından sonraki kullanılabilir genişliği aşmaz | Taşmayı önler; px sınırı Bölüm 2'deki kenar boşluğu formülünden hesaplanır |

### 8.2 Dikey A4 — `{Rapor}.Dikey.frx`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp" DoublePass="true"
        ReportInfo.Created="01/01/2026 00:00:00" ReportInfo.Modified="01/01/2026 00:00:00"
        ReportInfo.CreatorVersion="2026.2.3" ReportInfo.Version="1.0.0" ReportInfo.Description="İlk sürüm">
  <Dictionary>
    <TableDataSource Name="Satirlar" ReferenceName="Data.Satirlar" DataType="System.Int32" Enabled="true">
      <Column Name="SiraNo"   DataType="System.Int32"/>
      <Column Name="Aciklama" DataType="System.String"/>
      <Column Name="Tutar"    DataType="System.Decimal"/>
    </TableDataSource>
    <Parameter Name="BaslikAdi"   DataType="System.String"/>
    <Parameter Name="OlusturanAd" DataType="System.String"/>
    <Parameter Name="Tarih"       DataType="System.String"/>
    <Parameter Name="BelgeNo"     DataType="System.String"/>
    <Parameter Name="SablonSurum" DataType="System.String"/>
    <Parameter Name="Filigran"    DataType="System.String"/>
    <Total Name="ToplamTutar" Expression="[Satirlar.Tutar]" Evaluator="Data1" PrintOn="ReportSummary1"/>
  </Dictionary>

  <ReportPage Name="Page1" Landscape="false" PaperWidth="210" PaperHeight="297"
              LeftMargin="10" TopMargin="10" RightMargin="10" BottomMargin="10"
              Watermark.Font="Arial, 60pt">

    <!-- Kurumsal üst bilgi: her sayfada -->
    <PageHeaderBand Name="PageHeader1" Width="718.2" Height="47.25">
      <PictureObject Name="PicLogo" Width="94.5" Height="37.8"/>
      <TextObject Name="TxtBelgeNo" Left="518.4" Width="199.8" Height="18.9" Text="Belge No: [BelgeNo]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
      <TextObject Name="TxtTarih" Left="518.4" Top="18.9" Width="199.8" Height="18.9" Text="[Tarih]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
    </PageHeaderBand>

    <!-- Rapor başlığı: yalnızca ilk sayfada -->
    <ReportTitleBand Name="ReportTitle1" Width="718.2" Height="37.8">
      <TextObject Name="TxtBaslik" Width="718.2" Height="37.8" Text="[BaslikAdi]"
                  HorzAlign="Center" VertAlign="Center" Font="Arial, 16pt, style=Bold"/>
    </ReportTitleBand>

    <!-- Veri satırı + tablo başlığı (her sayfada tekrar eder) -->
    <DataBand Name="Data1" Width="718.2" Height="22.68" CanGrow="true" DataSource="Satirlar">
      <TextObject Name="ColSira" Width="56.7" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.SiraNo]" HorzAlign="Center" VertAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="ColAciklama" Left="56.7" Width="476.7" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.Aciklama]" VertAlign="Center" WordWrap="true" CanGrow="true" Font="Arial, 9pt"/>
      <TextObject Name="ColTutar" Left="533.4" Width="184.8" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.Tutar]" HorzAlign="Right" VertAlign="Center"
                  Format="Number" Format.UseLocale="false" Format.DecimalDigits="2"
                  Format.DecimalSeparator="," Format.GroupSeparator="." Format.NegativePattern="1"
                  Font="Arial, 9pt"/>

      <DataHeaderBand Name="DataHeader1" Width="718.2" Height="28.35" RepeatOnEveryPage="true">
        <TextObject Name="HdrSira" Width="56.7" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Sıra" HorzAlign="Center" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
        <TextObject Name="HdrAciklama" Left="56.7" Width="476.7" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Açıklama" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
        <TextObject Name="HdrTutar" Left="533.4" Width="184.8" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Tutar" HorzAlign="Right" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
      </DataHeaderBand>
    </DataBand>

    <!-- Toplam + imza + QR -->
    <ReportSummaryBand Name="ReportSummary1" Width="718.2" Height="132.3" KeepWithData="true">
      <TextObject Name="TxtToplam" Left="533.4" Width="184.8" Height="22.68" Border.Lines="All"
                  Text="[ToplamTutar]" HorzAlign="Right" VertAlign="Center"
                  Format="Number" Format.UseLocale="false" Format.DecimalDigits="2"
                  Format.DecimalSeparator="," Format.GroupSeparator="." Font="Arial, 10pt, style=Bold"/>
      <TextObject Name="TxtImza1" Top="56.7" Width="226.8" Height="18.9" Text="Hazırlayan" HorzAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="TxtImza2" Left="245.7" Top="56.7" Width="226.8" Height="18.9" Text="Kontrol" HorzAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="TxtImza3" Left="491.4" Top="56.7" Width="226.8" Height="18.9" Text="Onaylayan" HorzAlign="Center" Font="Arial, 9pt"/>
      <BarcodeObject Name="Qr1" Left="0" Top="94.5" Width="37.8" Height="37.8"
                     Text="[BelgeNo]" AllowExpressions="true" Barcode="QR Code"/>
    </ReportSummaryBand>

    <!-- Alt bilgi -->
    <PageFooterBand Name="PageFooter1" Width="718.2" Height="18.9">
      <TextObject Name="TxtAltSol" Width="500" Height="18.9" Text="[OlusturanAd] · Şablon v[SablonSurum]" Font="Arial, 8pt"/>
      <TextObject Name="TxtSayfa" Left="518.4" Width="199.8" Height="18.9" Text="Sayfa [Page#] / [TotalPages#]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
    </PageFooterBand>
  </ReportPage>
</Report>
```

### 8.3 Yatay A4 — `{Rapor}.Yatay.frx` (tam dosya)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp" DoublePass="true"
        ReportInfo.Created="01/01/2026 00:00:00" ReportInfo.Modified="01/01/2026 00:00:00"
        ReportInfo.CreatorVersion="2026.2.3" ReportInfo.Version="1.0.0" ReportInfo.Description="İlk sürüm">
  <Dictionary>
    <TableDataSource Name="Satirlar" ReferenceName="Data.Satirlar" DataType="System.Int32" Enabled="true">
      <Column Name="SiraNo"   DataType="System.Int32"/>
      <Column Name="Aciklama" DataType="System.String"/>
      <Column Name="Tutar"    DataType="System.Decimal"/>
    </TableDataSource>
    <Parameter Name="BaslikAdi"   DataType="System.String"/>
    <Parameter Name="OlusturanAd" DataType="System.String"/>
    <Parameter Name="Tarih"       DataType="System.String"/>
    <Parameter Name="BelgeNo"     DataType="System.String"/>
    <Parameter Name="SablonSurum" DataType="System.String"/>
    <Parameter Name="Filigran"    DataType="System.String"/>
    <Total Name="ToplamTutar" Expression="[Satirlar.Tutar]" Evaluator="Data1" PrintOn="ReportSummary1"/>
  </Dictionary>

  <ReportPage Name="Page1" Landscape="true" PaperWidth="297" PaperHeight="210"
              LeftMargin="10" TopMargin="10" RightMargin="10" BottomMargin="10"
              Watermark.Font="Arial, 60pt">

    <PageHeaderBand Name="PageHeader1" Width="1047.06" Height="47.25">
      <PictureObject Name="PicLogo" Width="94.5" Height="37.8"/>
      <TextObject Name="TxtBelgeNo" Left="847.26" Width="199.8" Height="18.9" Text="Belge No: [BelgeNo]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
      <TextObject Name="TxtTarih" Left="847.26" Top="18.9" Width="199.8" Height="18.9" Text="[Tarih]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
    </PageHeaderBand>

    <ReportTitleBand Name="ReportTitle1" Width="1047.06" Height="37.8">
      <TextObject Name="TxtBaslik" Width="1047.06" Height="37.8" Text="[BaslikAdi]"
                  HorzAlign="Center" VertAlign="Center" Font="Arial, 16pt, style=Bold"/>
    </ReportTitleBand>

    <DataBand Name="Data1" Width="1047.06" Height="22.68" CanGrow="true" DataSource="Satirlar">
      <TextObject Name="ColSira" Width="56.7" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.SiraNo]" HorzAlign="Center" VertAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="ColAciklama" Left="56.7" Width="805.56" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.Aciklama]" VertAlign="Center" WordWrap="true" CanGrow="true" Font="Arial, 9pt"/>
      <TextObject Name="ColTutar" Left="862.26" Width="184.8" Height="22.68" Border.Lines="All" GrowToBottom="true"
                  Text="[Satirlar.Tutar]" HorzAlign="Right" VertAlign="Center"
                  Format="Number" Format.UseLocale="false" Format.DecimalDigits="2"
                  Format.DecimalSeparator="," Format.GroupSeparator="." Format.NegativePattern="1"
                  Font="Arial, 9pt"/>

      <DataHeaderBand Name="DataHeader1" Width="1047.06" Height="28.35" RepeatOnEveryPage="true">
        <TextObject Name="HdrSira" Width="56.7" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Sıra" HorzAlign="Center" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
        <TextObject Name="HdrAciklama" Left="56.7" Width="805.56" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Açıklama" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
        <TextObject Name="HdrTutar" Left="862.26" Width="184.8" Height="28.35" Border.Lines="All" Fill.Color="Gainsboro"
                    Text="Tutar" HorzAlign="Right" VertAlign="Center" Font="Arial, 9pt, style=Bold"/>
      </DataHeaderBand>
    </DataBand>

    <ReportSummaryBand Name="ReportSummary1" Width="1047.06" Height="132.3" KeepWithData="true">
      <TextObject Name="TxtToplam" Left="862.26" Width="184.8" Height="22.68" Border.Lines="All"
                  Text="[ToplamTutar]" HorzAlign="Right" VertAlign="Center"
                  Format="Number" Format.UseLocale="false" Format.DecimalDigits="2"
                  Format.DecimalSeparator="," Format.GroupSeparator="." Font="Arial, 10pt, style=Bold"/>
      <TextObject Name="TxtImza1" Top="56.7" Width="330" Height="18.9" Text="Hazırlayan" HorzAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="TxtImza2" Left="358.5" Top="56.7" Width="330" Height="18.9" Text="Kontrol" HorzAlign="Center" Font="Arial, 9pt"/>
      <TextObject Name="TxtImza3" Left="717" Top="56.7" Width="330" Height="18.9" Text="Onaylayan" HorzAlign="Center" Font="Arial, 9pt"/>
      <BarcodeObject Name="Qr1" Left="0" Top="94.5" Width="37.8" Height="37.8"
                     Text="[BelgeNo]" AllowExpressions="true" Barcode="QR Code"/>
    </ReportSummaryBand>

    <PageFooterBand Name="PageFooter1" Width="1047.06" Height="18.9">
      <TextObject Name="TxtAltSol" Width="800" Height="18.9" Text="[OlusturanAd] · Şablon v[SablonSurum]" Font="Arial, 8pt"/>
      <TextObject Name="TxtSayfa" Left="847.26" Width="199.8" Height="18.9" Text="Sayfa [Page#] / [TotalPages#]"
                  HorzAlign="Right" Font="Arial, 8pt"/>
    </PageFooterBand>
  </ReportPage>
</Report>
```

### 8.4 Kolon eklerken genişlik kuralı

1. Kolon genişliklerini Bölüm 2.2'den seç, **toplamı** hesapla.
2. Toplamı seçilen yönün kenar boşlukları sonrası kullanılabilir genişliğiyle karşılaştır (Bölüm 2.1); `718.2` ve `1047.06` yalnızca 10 mm boşluk örneğidir.
3. Her kolonun `Left` değeri, öncekilerin `Left + Width` toplamıdır.
4. **Uzun metin kolonu** (açıklama) kalan boşluğu alır; son kolonun `Left + Width` kullanılabilir genişliği aşmamalıdır.
5. Başlık ve veri satırında `Left`/`Width` değerleri **birebir aynı** olmalı.

---

## 9. Şablon Kütüphanesi (Ö6)

Yapay zeka sayfa tipine göre aşağıdaki iskeletlerden birini seçer; hepsi Bölüm 8.1 kurallarına uyar.

| Tip | Ne zaman | Band yapısı | Önerilen yön |
|---|---|---|---|
| **Liste** | Tek tablo, düz satırlar | PageHeader → ReportTitle → DataBand (+DataHeader) → ReportSummary → PageFooter | Genişliğe göre |
| **Ana-Detay** | Bir kayıt + alt satırları (talimat + kalemler) | ReportTitle → DataBand(ana) → iç içe DataBand(detay + DataHeader) | Dikey |
| **Gruplu (ara toplamlı)** | Birim/kategori bazlı gruplama | ReportTitle → GroupHeaderBand → DataBand → GroupFooterBand (ara toplam) → ReportSummary (genel toplam) | Yatay |
| **Makbuz / Tek kayıt** | Tek belge (form görünümü) | PageHeader → ReportTitle → DataBand (1 satır, alan–değer ızgarası) → imza bloğu | Dikey |
| **Ek liste (çok sayfalı)** | Çok sayıda satırlı ek | PageHeader → DataBand (+DataHeader, `RepeatOnEveryPage`) → PageFooter; özet yok | Yatay |

İskelet parçaları:

```xml
<!-- Gruplu: GroupHeaderBand içinde DataBand ve GroupFooterBand -->
<GroupHeaderBand Name="GroupHeader1" Width="718.2" Height="22.68" Condition="[Satirlar.Birim]" SortOrder="Ascending">
  <TextObject Name="TxtGrup" Width="718.2" Height="22.68" Text="[Satirlar.Birim]" Fill.Color="WhiteSmoke" Font="Arial, 9pt, style=Bold"/>
  <DataBand Name="Data1" Width="718.2" Height="22.68" CanGrow="true" DataSource="Satirlar">
    <!-- hücreler -->
  </DataBand>
  <GroupFooterBand Name="GroupFooter1" Width="718.2" Height="22.68">
    <TextObject Name="TxtAraToplam" Left="533.4" Width="184.8" Height="22.68" Text="[AraToplam]" HorzAlign="Right" Font="Arial, 9pt, style=Bold"/>
  </GroupFooterBand>
</GroupHeaderBand>
<!-- Dictionary içine: <Total Name="AraToplam" Expression="[Satirlar.Tutar]" Evaluator="Data1" PrintOn="GroupFooter1" ResetOnReprint="true"/> -->

<!-- Ana-Detay: Dictionary'ye ilişki + iç içe DataBand -->
<!-- <Relation Name="TalimatKalem" ParentDataSource="Talimat" ChildDataSource="Kalemler" ParentColumns="Id" ChildColumns="TalimatId"/> -->
<DataBand Name="DataAna" Width="718.2" Height="37.8" DataSource="Talimat">
  <!-- ana alanlar -->
  <DataBand Name="DataDetay" Width="718.2" Height="22.68" CanGrow="true" DataSource="Kalemler">
    <!-- detay hücreleri -->
  </DataBand>
</DataBand>
```

> Bu parçalar iskelettir; `RaporTanimi.Tablolar` listesine tüm tabloları (ör. `"Talimat", "Kalemler"`) ve DataSet'e ilişkiyi eklemeyi unutma.

---

## 10. İsteğe Bağlı Kurumsal Öğeler, Belge No ve Denetim Kaydı (Ö5, Ö7)

### 10.1 Rapor ihtiyacına göre seçilen öğeler

| Öğe | Nesne adı | Yer | Kaynak |
|---|---|---|---|
| Logo | İhtiyaca göre adlandır | PageHeader veya rapor başlığı | Yalnızca mevcut görsel varlığı ve dağıtım yolu doğrulanırsa ekle |
| Belge no | İhtiyaca göre adlandır | Başlık/üst bilgi | Yalnızca belge numarası üretimi ve benzersizlik kuralı tanımlıysa |
| Filigran | `Page1.Watermark` veya FRX nesnesi | Sayfa | Taslak/onay durumunu veri modelinden güvenilir biçimde belirle |
| İmza bloğu | İhtiyaca göre adlandır | Uygun band | Belge iş akışına göre seç; tüm raporlara aynı imza alanlarını dayatma |
| Alt bilgi | İhtiyaca göre adlandır | PageFooter | Sayfa numarası ve iş için gerekli bilgi; toplam sayfa gösterilecekse `DoublePass`'ı doğrula |
| QR | İhtiyaca göre adlandır | Belge düzenine uygun band | Hedef/doğrulama endpoint'i, erişim ve kişisel/finansal veri sızıntısı riski değerlendirilmeden ekleme |

> Bu öğeler ortak servis tarafından zorunlu olarak rapora eklenmez. İlgili `.frx` alanları varsa parametreleri raporun veri hazırlama katmanından sağlanır.

### 10.2 Belge numarası

| Alan | Kural |
|---|---|
| Biçim | İş gereksinimi belirlenirse tekil ve çakışmaya dayanıklı bir biçim tanımla |
| Üretim | Seçilen veritabanı sağlayıcısında atomik/benzersiz üretim kuralı sağla; yalnızca log satırına bağlı varsayma |
| Basım | İlgili belge formatı gerektiriyorsa; QR kullanımı isteğe bağlıdır |

### 10.3 İsteğe bağlı rapor çıktı denetim kaydı

| Alan | Örnek içerik | Açıklama |
|---|---|---|
| Id | Sağlayıcının PK tipi | Projenin veritabanı sağlayıcısına uygun |
| Modul, Rapor | Uygulama metni | Örn. `OdemeTalimat` |
| KayitId | Alanı tanımlayan kayıt anahtarı | Gerçek entity anahtar tipiyle uyumlu |
| Yon, Format | `Dikey` / `Pdf` vb. | Yalnızca sunulan seçenekler |
| SablonSurum | FRX sürümü varsa | `.frx` `ReportInfo.Version` |
| Kullanici, Tarih | Uygulama kullanıcısı, UTC zaman | Saklama ve erişim politikası belirlenmeli |
| IpAdresi | İsteğe bağlı | Gereklilik ve kişisel veri saklama politikası varsa |

```csharp
public interface IRaporCiktiLogServisi
{
    /// Yalnızca çıktı denetim kaydı gereksinimi olan raporlarda kullanılır.
    Task KaydetAsync(RaporTanimi tanim, string kayitId, RaporYonu yon, string format, ClaimsPrincipal kullanici);
}
```

> GamabelMVC'nin mevcut raporları için bu tablo/servis veya SQL Server'a özgü kolon tipleri var kabul edilmez. Eklenecekse proje veritabanı sağlayıcısı, migration yaklaşımı, erişim politikası ve saklama süresi önce belirlenir.

### 10.4 Belge doğrulama (isteğe bağlı)

QR, `/RaporDogrula/{belgeNo}` adresine yönlendirebilir. Bu sayfa **yalnızca** belge no, tarih, modül ve geçerlilik bilgisini gösterir; **tutar veya kişi verisi göstermez** ve giriş gerektirir.

---

## 11. Türkçe Karakter, Font ve Biçim (Ö3)

| Konu | Kural |
|---|---|
| Dosya kodlaması | `.frx` **UTF-8** (BOM'suz ya da BOM'lu) |
| Font | `Arial`; Linux sunucuda yoksa `Liberation Sans` (metrik uyumlu) kur. Yeni fontta ı, İ, ş, Ş, ğ, Ğ, ü, ö, ç harflerini test et |
| Sayı biçimi | `Format.UseLocale="false"`, `DecimalSeparator=","`, `GroupSeparator="."`, `DecimalDigits="2"` → `1.234,50` |
| Tarih biçimi | `Format="Date" Format.Format="dd.MM.yyyy"` veya parametre olarak hazır metin |
| Kültür | `UseRequestLocalization` ile `tr-TR` varsayılan; ayrıca biçimler `.frx`'te açık yazıldığından sunucu kültüründen etkilenmez |
| Büyük/küçük harf | `ToUpper()` yerine `ToUpper(new CultureInfo("tr-TR"))` (i → İ) |
| Test verisi | "ÇÖŞĞÜİ çöşğüı" içeren bir satırla PDF'i görsel kontrol et |

---

## 12. Excel Dışa Aktarma ve PDF Kısıtları (Ö8)

| Konu | Karar |
|---|---|
| Excel çıktısı | **Önce** FastReport OpenSource paketinde XLSX export desteğini doğrula. Yoksa tablo verisini (`DataSet`) `ClosedXML` ile doğrudan `.xlsx`'e yaz; `.frx`'ten **üretilmez** |
| Excel içeriği | Düz veri tablosu (başlık satırı + satırlar), toplam satırı formüllü; imza/QR yok |
| PDF kısıtları | `PDFSimpleExport` parola/kopyalama izni **desteklemeyebilir**; desteklenmiyorsa bunu kullanıcıya bildir. Alternatif: belge no + QR ile doğrulanabilirlik (Bölüm 10) |
| Excel route | `/{Modul}/RaporExcel/{id}` — aynı mevcut yetki/kapsam; çıktı logu yalnızca yapılandırılmışsa |

---

## 13. Büyük Veri ve Performans (Ö9)

| Satır sayısı | Yöntem |
|---|---|
| Küçük/orta hacim | Ölçülen yanıt süresi ve bellek kullanımı kabul edilebilirse mevcut senkron akış |
| Büyük hacim veya timeout/bellek baskısı ölçülmüş | Arka plan üretimi değerlendir; satır sayısı eşiğini test/ölçümle belirle |

> `5.000` satır evrensel bir eşik değildir. Rapor karmaşıklığı, veri büyüklüğü ve çalışma ortamı ölçülmeden tüm projeye sabit eşik koymayın. GamabelMVC'de arka plan kuyruğu/indirme akışı doğrulanmış mevcut özellik olarak varsayılmaz.

Arka plan akışı:

| Adım | Endpoint | Not |
|---|---|---|
| 1 | `POST /{Modul}/RaporPdfBaslat/{id}?yon=` | Yetki kontrolü, iş kuyruğa (`Channel<RaporIsi>` + `BackgroundService`) eklenir, `isId` döner |
| 2 | `GET /{Modul}/RaporDurum/{isId}` | `Bekliyor / Hazirlaniyor / Hazir / Hata` |
| 3 | `GET /{Modul}/RaporIndir/{isId}` | Yalnızca işi başlatan kullanıcı indirir; geçici dosya **15 dk** sonra silinir |

Önbellek (yalnızca performans ölçümü fayda gösteriyor ve güvenli invalidation anahtarı üretilebiliyorsa):

```csharp
var anahtar = $"pdf:{Tanim.Modul}:{id}:{y}:{kayitSurumAnahtari}:{surum}";
if (!_cache.TryGetValue(anahtar, out byte[]? pdf))
{
    pdf = _rapor.Pdf(Tanim, y, ds, prm).Icerik;
    _cache.Set(anahtar, pdf, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) });
}
```

> `kayitSurumAnahtari` gerçek model/veri katmanının güvenilir güncelleme belirtecidir; projede `GuncellemeTarihi` alanı varmış gibi varsaymayın. İlişkili satır değişiklikleri de anahtarı geçersiz kılmalıdır. Yetki kontrolü önbellekten **önce** yapılır. Hassas raporlarda önbelleği varsayılan olarak açmayın; istemciye giden yanıt `no-store` kalır.

---

## 14. Şablon Sürümleme (Ö10)

| Kural | Uygulama |
|---|---|
| Sürüm numarası | `ReportInfo.Version="MAJOR.MINOR.PATCH"` |
| Değişiklik notu | `ReportInfo.Description` içine son değişikliğin kısa özeti |
| Ne zaman artar | Kolon/yerleşim değişirse **MINOR**, hata düzeltmesi **PATCH**, yön/yapı değişirse **MAJOR** |
| Çıktıda görünür | Alt bilgi: `Şablon v1.0.0` |
| Log'a yazılır | Çıktı logu uygulanmışsa sürüm alanına |
| Dikey/Yatay | İki yön mevcutsa aynı veri alanı/iş sürümünü belgeleyin; farklı yerleşim değişikliklerini açıklamaya kaydedin |

---

## 15. Otomatik Doğrulama (Ö4)

### 15.1 `.frx` statik denetim testleri

```csharp
using System.Globalization;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

public class FrxA4Testleri
{
    private static string ReportsKlasoru => /* proje Reports yolu */ "Reports";

    [Theory]
    // Yalnızca geçişi tamamlanmış ve gerçekten mevcut olan şablonları listeleyin.
    [InlineData("OdemeTalimat/OdemeTalimat.Dikey.frx", "false", "210", "297")]
    [InlineData("OdemeTalimat/OdemeTalimat.Yatay.frx", "true",  "297", "210")]
    public void Frx_A4_Kurallarina_Uyar(string goreliYol, string landscape, string pw, string ph)
    {
        var dosya = Path.Combine(ReportsKlasoru, goreliYol);
        Assert.True(File.Exists(dosya), $"Test girdisi bulunamadı: {dosya}");

        var doc = XDocument.Load(dosya);
        var sayfa = doc.Descendants("ReportPage").Single();
        Assert.Equal(landscape, (string?)sayfa.Attribute("Landscape"));
        Assert.Equal(pw, (string?)sayfa.Attribute("PaperWidth"));
        Assert.Equal(ph, (string?)sayfa.Attribute("PaperHeight"));

        // Kenar boşluklarını mm'den çıkar; içerik genişlikleri FastReport px birimindedir.
        var kullanilabilirPx =
            (Px((string?)sayfa.Attribute("PaperWidth")!) -
             Px((string?)sayfa.Attribute("LeftMargin") ?? "0") -
             Px((string?)sayfa.Attribute("RightMargin") ?? "0")) * 3.78;

        // DoublePass yalnızca şablon toplam sayfa alanını kullanıyorsa zorunludur.
        var toplamSayfaKullaniliyor = doc.Descendants()
            .Any(e => (string?)e.Attribute("Text") is string text &&
                      text.Contains("[TotalPages", StringComparison.OrdinalIgnoreCase));
        if (toplamSayfaKullaniliyor)
            Assert.Equal("true", (string?)doc.Root!.Attribute("DoublePass"));

        foreach (var band in sayfa.Descendants().Where(e => e.Name.LocalName.EndsWith("Band")))
        {
            var w = (string?)band.Attribute("Width");
            if (w != null)
                Assert.True(Px(w) <= kullanilabilirPx + 0.01, $"{dosya}: {band.Name} genişliği taşıyor");
        }

        // Top/Left/Width geometrisini elle yeniden kurcalamak yerine,
        // nesnelerin kullanılabilir alan dışına taşmadığını denetle.
        foreach (var o in sayfa.Descendants().Where(e => e.Name.LocalName.EndsWith("Object")))
        {
            var left = Px((string?)o.Attribute("Left") ?? "0");
            var w = Px((string?)o.Attribute("Width") ?? "0");
            Assert.True(left + w <= kullanilabilirPx + 0.01, $"{dosya}: {(string?)o.Attribute("Name")} sayfadan taşıyor");
        }
    }

    private static double Px(string s) => double.Parse(s, CultureInfo.InvariantCulture);
}
```

### 15.2 PDF boyut testi (`UglyToad.PdfPig` NuGet paketi ile)

```csharp
[Theory]
[InlineData(RaporYonu.Dikey)]
[InlineData(RaporYonu.Yatay)]
public void Pdf_A4_Boyutunda(RaporYonu yon)
{
    var cikti = servis.Pdf(RaporKaydi.OdemeTalimat, yon, TestVerisi(), TestParametreleri());
    using var pdf = UglyToad.PdfPig.PdfDocument.Open(cikti.Icerik);

    foreach (var s in pdf.GetPages())
    {
        var kisa = Math.Min(s.Width, s.Height);
        var uzun = Math.Max(s.Width, s.Height);
        Assert.InRange(kisa, 594, 596.5);     // A4 ≈ 595.28 pt
        Assert.InRange(uzun, 841, 843);       // A4 ≈ 841.89 pt
        Assert.Equal(yon == RaporYonu.Yatay, s.Width > s.Height);
    }
}
```

### 15.3 Test kapsamı ve elle görsel kontrol

Önce repoda mevcut test projesi/framework'ünü keşfet; yoksa xUnit veya başka test altyapısı varmış gibi varsayma. Test eklemek bu görevin kapsamındaysa yalnızca rapor şablonlarını ve çıktıyı kapsayan küçük bir test altyapısı kur. Statik kontrolleri yeni standarda geçirilmiş şablonlara uygula; henüz geçmemiş raporları ayrı envanterle belirt.

`.frx` dosyasını **FastReport Designer** ile aç → önizle/PDF üret → rapora uygun Türkçe karakter, uzun açıklama, sayfa bölünmesi, sayfa numarası ve varsa logo/QR/imza öğelerini gözle doğrula. Bir projenin raporunda bulunmayan QR/logo/imzayı test şartı yapma. Designer doğrulaması yapılamadıysa sonucu açıkça belirt.

---

## 16. Yapay Zekanın İzleyeceği Adımlar

Kullanıcı **"X sayfasındaki verileri raporla"** dediğinde sırayla:

1. **Çakışma taraması:** Mevcut `Rapor*` action'ları, view'lar, `.frx` dosyaları (Bölüm 3.1). Varsa kullanıcıya özet ver.
2. **Sayfayı analiz et:** Controller/view/model'den raporlanacak alanları ve tabloları çıkar.
3. **Yön belirle:** Kolon genişlikleri toplamına göre (Bölüm 2.3). Kullanıcı yön verdiyse onu kullan; belirtilmediyse veri yoğunluğu ve geriye dönük uyumluluğa göre gerekli yön(ler)i seç, gereksiz ikinci şablonu zorunlu kılma.
4. **Şablon tipini seç** (Bölüm 9) ve **kolon genişliklerini hesapla** (Bölüm 8.4).
5. **Ortak katmanı kontrol et:** `RaporTanimi`/`IRaporServisi`/`RaporServisi`/isteğe bağlı log servisi, `Views/Shared/Rapor.cshtml`, paket referansı, DI kayıtları (Bölüm 4–5, 7). Varsa dokunma, yoksa ihtiyaca göre oluştur.
6. **`.frx` dosyalarını yaz** (Bölüm 8): v3 kuralları, sürümleme ve raporun ihtiyaç duyduğu kurumsal öğeler.
7. **`RaporKaydi`'na tek satır ekle** (tablo adları `.frx` ile aynı).
8. **Controller'a gereken rapor action'larını ekle** (Bölüm 6): mevcut policy/kayıt kapsamı, gerekiyorsa `yon` doğrulaması, hassas çıktı için `no-store`; yalnızca talep edilmiş/uygunsa log. **Veri hazırlamayı gerçek modele ve mevcut servislere göre yap.**
9. **Sayfaya butonları ekle** (Bölüm 7).
10. **Derle, testleri çalıştır** (Bölüm 15), Designer'da aç. Çalıştıramadığın adımı özette **açıkça belirt**.
11. **Kullanıcıya özet ver:** Oluşan/değişen dosyalar, route'lar, taşınan eski dosyalar, uygulamayı yeniden başlatma uyarısı.

---

## 17. Doğrulama Kontrol Listesi

| # | Kontrol | Beklenen |
|---|---|---|
| 1 | `.frx` metadata | Yönlü yeni şablonlarda `ReportInfo.Version`; `DoublePass` yalnızca toplam sayfa alanı kullanılıyorsa |
| 2 | `PaperWidth/PaperHeight/Landscape` | Kullanılan rapor için A4 ve doğru yön |
| 3 | Band/obje genişlikleri | Kenar boşluklarına göre hesaplanan kullanılabilir alan içinde |
| 4 | Tekrarlanan tablo başlığı | Kullanılan band yapısında sayfa kırılımlarında doğru görünür |
| 5 | Band `Top` koordinatları | FRX/Designer koordinatları korunmuş; yalnızca görsel taşma varsa düzeltilmiş |
| 6 | Paketler | İhtiyaç duyulan paketler proje sürümüyle uyumlu, derleme başarılı; mevcut FastReport kaydı tekrarlanmamış |
| 7 | `Program.cs` | Mevcut `AddFastReport()` + `UseFastReport()` korunmuş; yeni DI kayıtları gerekli ve çakışmasız |
| 8 | FastReport Web statik kaynakları | Preview çalışmıyorsa ilgili CSS/JS istekleri uygulamada doğrulanmış |
| 9 | Yetki | İlgili mevcut policy ve birim/kayıt kapsamı korunmuş; yetkisiz kullanım engellenmiş |
| 10 | Hatalı `yon` | Yön parametresi endpoint tarafından destekleniyorsa HTTP 400; legacy endpoint davranışı bozulmamış |
| 11 | PDF yanıt başlıkları | Hassas çıktıda `Cache-Control: no-store` |
| 12 | Önizleme | İlgili mevcut preview, yazdırma/export ve varsa yön seçici davranışı doğrulanmış |
| 13 | PDF | A4, taşma yok, gereken sayfa numarası ve Türkçe karakterler doğru |
| 14 | Belge no / QR / log | Yalnızca bu rapor için gereksinim olarak seçilmiş ve uygulanmışsa doğrula |
| 15 | Testler | Mevcut test altyapısında ilgili testler geçti; test altyapısı yoksa durum ve Designer doğrulaması açıkça belirtilmiş |
| 16 | Eski route/view | Kullanım yerleri taranmış; uyumluluk gerektiren route/view korunmuş |

---

## 18. Sık Karşılaşılan Hatalar ve Çözümleri

| Belirti | Neden | Çözüm |
|---|---|---|
| Önizlemede yalnızca siyah yükleme daireleri | CSS/JS 404 | `wwwroot/_content/FastReport.Web/` altındaki `styles.min.css` ve `webreport-script.bundle.min.js` dosyalarını kontrol et |
| `RuntimeBinderException: null reference` | View'de `ViewBag.WebReport.GetHtml()` | `@model WebReport` + `@await Model.Render()` |
| `BLAZOR106` derleme hatası | Blazor `Toolbar.razor.js` dosyası projede | Dosyayı projeye dahil etme |
| `ArgumentOutOfRangeException` → `PreparedPages.GetPageSize(0)` önizlemede 500 | Rapor `Prepare()` sonrasında hiç sayfa üretmemiş olabilir (ör. boş DataBand) veya preview isteğinden önce `WebReport.Report` dispose edilmiştir | Boş liste için açık bir satır/boş durum bandı üret; gerçek kayıt sayısını ayrı tut. `Response.RegisterForDispose(web.Report)` ile raporu ilk yanıt bitiminde kapatma. Dolu ve boş veriyle preview/PDF test et. |
| `PDFSimpleExport` bulunamıyor | `PdfSimple` paketi yok / sürüm uyumsuz | Bölüm 4'teki paketi Web paketiyle aynı sürümde ekle |
| `InvalidOperationException: '…' tablosu DataSet içinde yok` | Tablo adı `.frx` `ReferenceName` ile uyuşmuyor | `DataTable.TableName` = `ReferenceName` son parçası; `RaporTanimi.Tablolar`'ı güncelle |
| Kolon başlıkları rapor başlığının üstünde | Band yerleşimi ve tekrar ayarı raporla uyuşmuyor | `DataHeaderBand` veya `PageHeaderBand` yerleşimini Designer'da düzelt; mevcut çalışan band tipini sırf adı nedeniyle değiştirme |
| "Sayfa 1 / " (toplam boş) | Toplam sayfa alanı şablonun değerlendirme ayarıyla uyumsuz | Kullanılan FastReport sayfa alanını/formatını ve gerekiyorsa `DoublePass`'ı örnek raporda doğrula |
| Uzayan satırda kenarlıklar kopuk | Büyüyen metin hücreleri bandın geri kalanıyla eşleşmiyor | `CanGrow`, `WordWrap`, gerekirse `GrowToBottom` ayarlarını örnek uzun satırla deneyerek düzelt |
| PDF'de yatay taşma | Kenar boşluğu sonrası kullanılabilir genişlik aşılmış olabilir | Bölüm 2'ye göre gerçek kullanılabilir alanı hesapla; PDF/Designer'da kontrol et |
| `FileNotFoundException` (frx) | Şablon yolu/çıktıya kopyalama yanlış | Mevcut ContentRoot/AppContext yolunu ve `.csproj` kopyalama kuralını doğrula; adlandırma geçişini uyumlulukla yap |
| Başkası id değiştirerek rapor görebiliyor | Kayıt/birim kapsamı kontrolü yok veya policy eksik | Modülün mevcut policy'si ve kayıt kapsamı denetimini birlikte doğrula |
| Eski PDF geliyor | Önbellek varsa invalidation anahtarı ilişkili veri değişimini kapsamıyor | Önce cache kullanımı gerçekten gerekli mi ölç; anahtarı gerçek güncelleme belirteciyle tasarla |
| Türkçe karakter bozuk / yanlış para biçimi | Font yok, kültür bağımlı biçim | Bölüm 11 |
| Değişiklikler görünmüyor | Eski DLL çalışıyor | Uygulamayı durdurup yeniden başlat, **Ctrl + F5** |
| `dotnet build` dosya kopyalama hatası (iç içe geçici klasörler) | Eski geçici çıktılar | Temiz çıktı klasörü veya `dotnet msbuild /t:Compile` |

---

## 19. Yapay Zekaya Verilecek Kısa Komut Örneği

```
OdemeTalimat/Detay sayfasındaki verileri raporla.
Standart: FASTREPORT_A4_STANDART.md (v3)
Yön: dikey ve yatay
Çıktı: önizleme + PDF
```

Beklenen sonuç: Mevcut ödeme talimatı rapor/route/veri hazırlama ve `PrsAdmin` yetkisi önce incelenir; talebe uygun şablon yön(ler)i eklenir. Gerekliyse ortak rapor servisine bağlanır, eski `RaporPdf2` önizleme davranışı korunur, ilgili butonlar doğrulanır ve çalıştırılan test/Designer kontrolleri özette bildirilir.
