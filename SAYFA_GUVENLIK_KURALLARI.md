# Yeni Sayfa Guvenlik Kurallari

Bu belge, GAMABELMVC uygulamasina yeni controller, sayfa, form veya AJAX endpoint'i eklerken uygulanacak guvenlik standartlarini tanimlar. Yeni sayfalar varsayilan olarak giris yapmis kullaniciya ve yalnizca gerekli role/modul iznine sahip kullaniciya acik olmalidir.

## Temel Kural

- Giris yapmis olmak tek basina yeterli degildir. Her sayfa dogru uygulama modulu, rol, menu izni ve gerekiyorsa kayit kapsamiyla korunur.
- Menu baglantisini gizlemek yetkilendirme degildir. Sunucu, dogrudan URL ve AJAX isteklerinde de erisimi reddetmelidir.
- Yetki kontrolu Razor gorunumunde degil, controller/action veya uygulama servisinde yapilir.
- Yetki dogrulanamiyorsa erisim reddedilir. Eksik rol, modul, birim veya sube bilgisi izin olarak yorumlanmaz.

## Mevcut Merkezi Altyapi

Kimlik dogrulama ve yetki kurallari `Program.cs` icinde tanimlidir; policy degerlendirmesini `Services/AppAuthorizationHandler.cs` yapar.

- MVC endpoint'leri icin fallback policy giris yapmis kullanici ister. Yeni sayfaya `AllowAnonymous` eklemeyin.
- Oturum hareketsizligi 15 dakikadir. Kimlik cookie'si kayar sureyle yenilenebilir; `auth_time` claim'i ile mutlak oturum omru 8 saattir.
- Kimlik cookie'si HttpOnly, Secure ve SameSite=Lax olarak ayarlanmistir. Uretim ortami HTTPS kullanmalidir.
- Giris yapmis isteklerin yanitlarina `no-store` onbellek basliklari eklenir.
- Cikis `POST` ve anti-forgery token ile yapilir.
- Oturumsuz istekler normal sayfa gezintisinde login ekranina yonlendirilir. `X-Requested-With: XMLHttpRequest` header'i gonderen AJAX isteklerinde `401`; yetkisi yetersiz kullanicida `403` doner. Header gondermeyen fetch istekleri login yonlendirmesi alabilir.
- PRS ve STS kimlikleri farkli modul claim'iyle ayrilir. Modul claim'i olmadan diger modul policy'si gecmez.

### Projede Tanimli Policy'ler

Yeni action'a uygun olan mevcut policy'yi kullanin; policy adlari buyuk/kucuk harfe duyarlidir.

| Policy | Amac |
|---|---|
| `PrsModule` | PRS modulunde giris yapmis kullanici |
| `StsModule` | STS modulunde giris yapmis kullanici; hassas action'larda ek rol/kapsam kontrolu gerekir |
| `PrsAdmin` | PRS `admin` rolu |
| `PrsMailAdmin` | PRS `admin` rolu; mail yonetimi |
| `ComplaintAdmin` | PRS `admin` rolu; sikayet yonetimi |
| `StsWarehouse` | STS `DepoSorumlusu` veya `Admin` |
| `StsAdmin` | STS `Admin` |
| `PrsMenuPersonel` | PRS personel menu izni veya admin |
| `PrsMenuPuantaj` | PRS puantaj menu izni veya admin |
| `PrsMenuRapor` | PRS rapor menu izni veya admin |
| `PrsMenuMesai` | PRS `admin` veya `birim_amiri` rolu ya da `menu_mesai` izni |
| `PrsMenuTatil` | PRS tatil menu izni veya admin |
| `PrsMenuOdeme` | PRS odeme menu izni veya admin; kullanmadan once ilgili controller'in mevcut rol kurallariyla tutarliligini denetleyin |
| `PrsMenuYetkilendirme` | PRS yetkilendirme menu izni veya admin |
| `PrsMenuDokumantasyon` | PRS dokumantasyon menu izni veya admin |

Policy listesinde olmayan yeni bir izin gerekiyorsa ad hoc string/Session kontrolu eklemeyin. Once `Program.cs` icinde policy ve gerekirse `AppAuthorizationHandler` gereksinimi tanimlayin; ardindan controller/action'a uygulayin.

## Yeni Sayfa Ekleme

### 1. Modulu ve erisim matrisini belirleyin

Kod yazmadan once bu bilgileri netlestirin:

- Sayfa PRS mi STS mi?
- Hangi roller okuyabilir, ekleyebilir, duzenleyebilir veya silebilir?
- Kullanici tum kayitlari mi, yalnizca kendi birim/sube kayitlarini mi gorebilir?
- Sayfa bir menu iznine mi bagli, yoksa yalnizca role mi bagli?

"Giris yapmis herkes" ile "her rol" ayni sey degildir; gerekiyorsa ayrica rol ve modul policy'si uygulayin.

### 2. Controller veya action'a policy ekleyin

Policy'yi controller seviyesinde uygulamak varsayilan tercihtir. Yalnizca belirli action'lar farkli erisim gerektiriyorsa action seviyesinde kullanin.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMenuPuantaj")]
public class OrnekController : Controller
{
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Kaydet(OrnekViewModel model)
    {
        // Kayit kapsam ve is kurali kontrollerini de burada yapin.
        return RedirectToAction(nameof(Index));
    }
}
```

STS icin policy adini ihtiyaca gore `StsModule`, `StsWarehouse` veya `StsAdmin` secin. `StsModule`, kullaniciyi yalnizca STS modulune baglar; depo yonetimi, siparis ve rapor gibi islemlerde tek basina yeterli olmayabilir.

### 3. Veri kapsam denetimini sunucuda yapin

Kullanici tarafindan gelen `birim`, `subeId`, `personelId`, kayit ID'si veya rol degerini yetki kaniti olarak kabul etmeyin. Degeri kimlik claim'inden veya veritabanindaki kayittan belirleyip istenen nesnenin kapsamiyla karsilastirin.

- PRS: Admin tum birimleri gorebilir; birim amirinin birim kapsaminda kalin. Istek farkli birim gonderse bile sorguyu kullanicinin yetkili birimine sabitleyin.
- STS: Sube personelinin `sube_id` claim'iyle sadece kendi subesinin kayitlarini okuyup degistirmesini saglayin. Depo/Admin kapsam genisletmesini ilgili role policy'sinden sonra uygulayin.
- Guncelleme/silme/onaylama isleminde yalnizca sayfayi korumak yetmez. Islem yapilacak kaydin kapsamini veritabanindan okuyup kullanici kapsamiyla dogrulayin.
- Mumkunse kapsam kosulunu veritabanindaki `UPDATE`/`DELETE` sorgusuna da ekleyin; ornegin kayit ID'siyle beraber yetkili `SubeId` veya `Birim` kosulunu kullanin.
- Kayit bulunamiyorsa veya kullanici kapsami belirsizse reddedin. `SubeId = 0`, bos birim ya da eksik claim'i izin olarak degerlendirmeyin.

### 4. Form ve istek yontemini koruyun

- Veri degistiren islemler `POST`, `PUT`, `PATCH` veya `DELETE` ile yapilmalidir; silme/cikis gibi islemleri GET linkine baglamayin.
- MVC POST action'larina `[ValidateAntiForgeryToken]` ekleyin. Razor form tag helper kullanin veya token'i acikca uretin.
- AJAX/fetch isteginde anti-forgery token'i header veya form verisiyle gonderin. JSON endpoint'lerinde token gonderimini istemci koduyla birlikte test edin.
- Yalnizca `HttpGet` endpoint'i olmasi, endpoint'in anonim veya zararsiz oldugu anlamina gelmez. GET action da uygun `[Authorize]` policy'sine tabi olmalidir.

## Anonim Erişim Istisnasi

`[AllowAnonymous]` tum yetki policy'lerini atlar. Yalnizca giris sayfalari veya urun gereksinimiyle herkesin erismesi gereken belirli bir action icin kullanin; mumkunse controller yerine action seviyesinde sinirlayin.

Su an `AccountController` sinifinin tamaminda `[AllowAnonymous]` vardir; dolayisiyla Login/StsLogin disindaki Register action'i da anonimdir. Bu dokuman bu mevcut kayit davranisini degistirmez. AccountController'a yeni korumali action eklenirse action seviyesinde `[Authorize]` veya uygun policy ekleyin.

Statik dosyalar (`wwwroot`) MVC fallback policy tarafindan korunmaz. Gizli dosyalari statik dosya olarak yayinlamayin; erisim denetimi gereken indirmeleri yetkili controller action'i uzerinden sunun.

## STS Login Yonlendirmesi

Oturumsuz istegin dogru STS giris ekranina yonlendirilmesi icin `Program.cs` icindeki `isStsRequest` controller listesi kullanilir. Yeni bir STS controller eklerken controller adini bu listeye ekleyin. Aksi halde policy yine giris ister ancak STS URL'si PRS login ekranina yonlenebilir.

## Tamamlama Kontrol Listesi

Her yeni sayfayi tamamlamadan once asagidakileri dogrulayin:

- [ ] Anonim dogrudan URL istegi PRS/STS giris ekranina yonleniyor.
- [ ] Dogru moduldeki izinli rol sayfayi acabiliyor.
- [ ] Baska moduldeki ayni isimli rol sayfaya giremiyor.
- [ ] Yetkisiz rol `403` aliyor veya uygulamanin uygun reddetme sayfasina yonlendiriliyor.
- [ ] Menu gizlense bile URL'yi elle yazmak yetkiyi asamiyor.
- [ ] Kullanici baska birim/sube ID'si gondererek veri okuyamiyor veya degistiremiyor.
- [ ] AJAX istekleri `X-Requested-With: XMLHttpRequest` gonderiyor ve login HTML'i yerine beklenen `401`/`403` durumunu aliyor.
- [ ] POST degisiklikleri anti-forgery token olmadan reddediliyor.
- [ ] Logout POST ile session ve kimlik cookie'sini siliyor.
- [ ] 15 dakika hareketsizlikten ve 8 saat mutlak omurden sonra korumali istek tekrar giris istiyor.
- [ ] Giris yapilmis hassas sayfalar tarayici geri dugmesiyle cikis sonrasi gosterilmiyor.

Rol ve zaman asimi testlerini mumkunse otomatik integration testlerine ekleyin. Gercek hesap/veritabaniyle dogrulanamayan senaryolari tamamlanmis kabul etmeyin.

## Mevcut Kapsam Notlari

- Bu kurallar yeni sayfalar icin standarttir; mevcut her action'in tam denetimden gectigi anlamina gelmez.
- PRS duz metin parola ve anonim kayit akisinin degistirilmesi bu belgenin kapsami disindadir.
- Yeni bir rol veya islem yetkisi eklenince hem policy, hem kayit kapsam kontrolu, hem menunun davranisi hem de test matrisi birlikte guncellenmelidir.
