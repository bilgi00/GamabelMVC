# FastReport Calisma Kaydi

**Tarih:** 24.08.2026
**Proje:** GamabelMVC
**Dal:** `main`

## Yapilan Islemler

Bugunku FastReport calismalarinda Odeme Talimati raporunun onizleme, yazdirma ve farkli formatlara disa aktarma akisi duzeltildi.

1. `PDF2` butonu, dogrudan PDF indirmek yerine FastReport onizleme/yazdirma sayfasini acacak sekilde duzenlendi.
2. Yeni `RaporPdf2/{id}` endpoint'i olusturuldu. Mevcut `Reports/OdemeTalimat/OdemeTalimat.frx` tasarimi kullanilmaya devam ediyor; A4 duzeni, tablo kolonlari, toplam ve imza alanlari korunuyor.
3. FastReport kaynaklarinin yuklenmesi icin `UseFastReport()` middleware'i eklendi ve pipeline icindeki sirasi duzeltildi.
4. Rapor gorunumu bagimsiz tam sayfa olacak sekilde duzenlendi. Rapor alani icin yuzde 100 genislik ve `calc(100vh - 24px)` yukseklik verildi; toolbar gorunur birakildi.
5. Rapor view'indeki fazladan `</div>` etiketi kaldirildi.
6. Guncel `FastReport.OpenSource.Web 2026.2.3` API'sine uyum saglandi. Eski makaledeki `GetHtml()`, `ReportFile`, `ShowToolbar`, `WebReportGlobals.Scripts()` ve `WebReportGlobals.Styles()` gibi uyumsuz kullanimlar dikkate alinmadi.
7. `ViewBag.WebReport.GetHtml()` kaynakli null cagrisi kaldirildi; rapor `Model.Render()` ile olusturuluyor.
8. FastReport 2026.2.3 icin eksik CSS, JavaScript ve kaynak ikonlari `wwwroot/_content/FastReport.Web/` altina eklendi.
9. Kullanilmayan Blazor `Toolbar.razor.js` dosyasinin derleme hatasina yol acmamasi icin kaldirilmasi saglandi.
10. FastReport toolbar ayarlari etkinlestirildi:
    - Yazdirma: `ShowPrint`
    - Disa aktarma menusu: `Exports.Show`
    - Hazirlanmis rapor/PDF secenegi: `ShowPreparedReport`

## Ilgili Dosyalar

- `Controllers/PRS/OdemeTalimatController.cs`
- `Views/PRS/OdemeTalimat/Detay.cshtml`
- `Views/PRS/OdemeTalimat/Rapor.cshtml`
- `Views/PRS/Mesai/FastReportPreview.cshtml`
- `Program.cs`
- `Reports/OdemeTalimat/OdemeTalimat.frx`
- `gamabelmvc.csproj`
- `wwwroot/_content/FastReport.Web/css/styles.min.css`
- `wwwroot/_content/FastReport.Web/js/webreport-script.bundle.min.js`

## Dogrulama

- Razor derlemesi ve proje derlemesi basarili oldu.
- Kayitlarda yeni derleme hatasi bulunmuyor.
- Mevcut projeden gelen 46 nullable/platform uyarisi devam ediyor.
- CSS ve JavaScript endpoint'leri `200` dondu.
- Uygulamanin guncel DLL'yi kullanmasi icin durdurulup yeniden baslatilmasi ve tarayicida `Ctrl + F5` ile yenilenmesi gerekiyor.

## Not

Proje `FineReport` degil `FastReport` kullaniyor. Bu nedenle mevcut cikti FastReport'un `.frx` rapor tasarimina dayanir; gercek FineReport `.cpt` formati bu calismanin kapsaminda degildir.

## Son Duzeltmeler

- `Rapor.cshtml` icindeki `ViewBag.WebReport.GetHtml()` cagrisi kaldirildi. Controller `WebReport` nesnesini model olarak gonderdigi icin rapor artik yalnizca `@await Model.Render()` ile olusturuluyor. Bu degisiklik `RuntimeBinderException: Cannot perform runtime binding on a null reference` hatasini giderdi.
- `OdemeTalimatController.Rapor` action'inda FastReport toolbar ayarlari acikca etkinlestirildi: `Toolbar.Show`, `Toolbar.ShowPrint`, `Toolbar.Exports.Show` ve `Toolbar.Exports.ShowPreparedReport`.
- FastReport paketinde bulunmayan web assetleri `wwwroot/_content/FastReport.Web/` altina yerlestirildi. Blazor'a ozel `Toolbar.razor.js` dosyasi MVC derlemesinde `BLAZOR106` hatasi verdigi icin dahil edilmedi.
- Son derleme gecici cikti klasorunde basarili oldu. Calisan uygulama eski DLL'yi kullaniyorsa uygulama yeniden baslatilmali ve tarayici `Ctrl + F5` ile yenilenmelidir.

## Sorun Teshisi ve Son Duzeltme

- Rapor sayfasi tarayicida acildiginda yalnizca siyah yukleme daireleri goruldu. Oturum acilmis tarayici ile `http://localhost:5010/OdemeTalimat/Rapor/54` adresi test edildi.
- Tarayici gelistirici olaylarinda su iki istegin `404 Not Found` dondugu tespit edildi:
    - `/_content/FastReport.Web/css/styles.min.css`
    - `/_content/FastReport.Web/js/webreport-script.bundle.min.js`
- Bu dosyalarin `FastReport.OpenSource.Web 2026.2.3` NuGet paketinin icinde bulunmadigi, paketin yalnizca DLL ve XML dosyalari icerdigi kontrol edildi.
- Eksik FastReport web asset'leri `wwwroot/_content/FastReport.Web/` altinda servis edilecek sekilde projeye eklendi. Bu kaynaklar yuklenmeden toolbar ve rapor sayfasi JavaScript'i calismadigi icin onizleme bos kaliyor.
- `UseFastReport()` middleware'inin gorevi yalnizca route eklemek degil; `WebReport.ResourceLoader` ve FastReport internal controller yapisini da baslatmaktir. Bu nedenle `AddFastReport()` sonrasinda `UseFastReport()` mutlaka pipeline'a eklenmelidir.
- Paylasilan eski MVC makalesindeki `WebReportGlobals.Scripts()`, `WebReportGlobals.Styles()`, `GetHtml()`, `ReportFile`, `ShowToolbar`, `Server.MapPath()` ve `PDFExport` API'leri bu OpenSource 2026.2.3 projesine uygulanmadi. Guncel kullanim `new WebReport`, `Report.Load(...)`, `@await Model.Render()` ve `UseFastReport()` seklindedir.
- Rapor view'i genel layout'tan bagimsiz tutuldu; boyutlari controller'da `Width = "100%"`, `Height = "calc(100vh - 24px)"` ve `Inline = false` olarak ayarlandi.
- Son derlemede kaynak ve Razor compile basarili oldu. Normal `dotnet build` sirasinda onceki gecici cikti klasorlerinin ic ice kopyalanmasindan kaynaklanan dosya kopyalama hatasi gorulebildigi icin temiz bir cikti klasoru veya `dotnet msbuild /t:Compile` kullanilmalidir.
