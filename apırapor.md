# API2 Birimler — İşlem ve Test Raporu

**Rapor tarihi:** 17.11.2026  
**Amaç:** `birimler` tablosunu `MyConnection` üzerinden okuyan API2 uç noktasını eklemek, yerel/veritabanı bağlantısını ve yayın ortamındaki 503 yanıtını incelemek.

## Özet

- `GET /api2/birimler` uç noktası eklendi.
- Yerel browser testinde API2, geçerli Admin JWT ile veritabanından **32 birim** döndürdü; yanıt şekli doğrulandı.
- Son `MyConnection` denemesinde `SELECT 1` başarılı oldu.
- `https://gamabel.xyz/api2/birimler` için görülen **503**, MySQL sorgusundan önce `MobileApi__Jwt__SigningKey` ayarı eksik olduğu için oluşuyor.
- Plesk paneline ayar kaydedilmedi; panel oturumu/erişimi mevcut değildi.
- Veritabanında test kullanıcısı **oluşturulmadı** ve şema değiştirilmedi.

## Yapılan kod değişiklikleri

- Yanıt DTO’su eklendi: [Api2BirimResponse.cs](./Api/Api2BirimResponse.cs)
- `MyConnection` ile `birimler.id` ve `birimler.birim_adi` okuyan servis eklendi: [BirimDirectoryService.cs](./Services/Api2/BirimDirectoryService.cs)
- Servis arayüzü eklendi: [IBirimDirectoryService.cs](./Services/Api2/IBirimDirectoryService.cs)
- `GET /api2/birimler` controller’ı eklendi: [BirimlerController.cs](./Controllers/Api2/BirimlerController.cs)
- Servis DI kaydı [Program.cs](./Program.cs) içine eklendi.
- API açıklaması [MOBILE_API.md](./MOBILE_API.md), istek örneği [mobile-api.http](./mobile-api.http) içine eklendi.

Uç nokta mevcut API2 güvenlik düzenini izler: geçerli bearer JWT ve `Admin` rolü gerekir. Sorgu salt okunurdur ve sonuçları `birim_adi`, ardından `id` sırasına göre listeler.

## Derleme

`dotnet build gamabelmvc.csproj --no-restore` başarılı oldu:

- Hata: **0**
- Uyarı: **1**, mevcut `NU1608` paket sürümü uyumsuzluğu (`Microsoft.CodeAnalysis.VisualBasic 4.0.1` ve çözümlenen `Microsoft.CodeAnalysis.Common 4.8.0`). API2 değişikliğiyle ilgili değildir.

## Browser ve API testleri

### Yerel API2 browser testi — 16.11.2026

Uygulama, geliştirme başlangıç seed işlemini çalıştırmamak için Production profili ve yalnızca test süresince kullanılan yerel bir JWT anahtarıyla başlatıldı. Anahtar kalıcı dosyaya yazılmadı.

| İstek | Sonuç |
|---|---|
| `GET /api2/birimler` — tokensız | `401 Unauthorized` |
| `GET /api2/birimler` — geçerli imzalı Admin test token’ı | `200 OK` |
| JSON yanıtı | 32 kayıt; her kayıtta tam sayı `id` ve metin `birimAdi` doğrulandı |

Bu test sırasında veritabanına yazma yapılmadı. Geçici uygulama testi bitince durduruldu. Browser’daki portal sayfasında ayrıca eksik `wwwroot/gamabelmvc.styles.css` kaynaklı bir hata görüldü; API2 isteğinin başarılı yanıtından bağımsızdır.

### MyConnection bağlantı denemeleri — 17.11.2026

Bağlantı dizesi değerleri rapora eklenmemiştir.

| Deneme | Sonuç |
|---|---|
| Sabah ilk doğrudan MySQL bağlantısı | `Unable to connect to any of the specified MySQL hosts` |
| Test kullanıcısı oluşturma hazırlığı sırasında bağlantı | Aynı bağlantı hatası; insert aşamasına geçilmedi |
| Doğrudan bağlantı ve `SELECT 1` denemesi | Başarısız |
| Daha sonraki tekrar, aynı `MyConnection` kaynağıyla | **Başarılı** — `SELECT 1` sonucu `1` |

Son başarılı deneme yalnızca bağlantı ve salt okunur `SELECT 1` yaptı; veritabanında kayıt eklemedi veya değiştirmedi.

## Yerel yapılandırma ve test yardımcıları

- `appsettings.Development.json` içinde `ConnectionStrings:MyConnection` tanımlıydı; kontrol sırasında uzak sunucu türünde adres ve 3306 portu kullanılıyordu. `ConnectionStrings__MyConnection` ortam değişkeni tanımlı değildi.
- [test-db.ps1](./test-db.ps1), [check-users.csx](./check-users.csx) ve [create-test-user.csx](./create-test-user.csx) kendi `localhost` bağlantı dizelerini kullanıyor; `MyConnection` ayarını okumuyorlar. Bu nedenle bu yardımcılarla yapılan test, uygulamanın uzak MySQL bağlantısını test etmez.
- Bu yardımcı dosyalarda bağımsız, sabit bağlantı bilgileri bulunduğu için bunları kullanmayın veya değerlerini paylaşmayın. Bu rapora hiçbir bağlantı parolası eklenmemiştir.

## Yayın ortamındaki 503

`https://gamabel.xyz/api2/birimler` isteğinde aşağıdaki yanıt gözlendi:

```json
{
  "title": "Service Unavailable",
  "status": 503,
  "detail": "The API2 service is not configured."
}
```

Uygulamadaki `MobileApiConfigurationMiddleware`, `/api2` isteklerinde `MobileApi:Jwt:SigningKey` değerinin en az 32 UTF-8 baytı olup olmadığını kontrol eder. Değer yoksa isteği veritabanı sorgusuna geçmeden `503` ile durdurur. Bu nedenle bu 503, MySQL kullanıcı yetkisi veya tablo sütunlarıyla ilgili bir hata değildir.

## Plesk yapılandırması

- Plesk için `web.config` içindeki `<aspNetCore><environmentVariables>` ayarının uygun yöntem olabileceği açıklandı.
- Workspace’te kaynak `web.config` bulunmadı. Plesk’e doğrudan erişim olmadığı için sunucu dosyası veya panel ayarı değiştirilemedi.
- `deploy-manifest.json` bir dağıtım dosya listesi; Plesk ortam değişkenlerini yönetmiyor.
- Üretim JWT anahtarı üretildi ve kullanıcıya panelde `MobileApi__Jwt__SigningKey` adıyla kaydedilmesi için gösterildi. Anahtar rapora ve kaynak dosyalara kopyalanmadı; Plesk’e kaydedildiği doğrulanmadı.
- Plesk paneli paylaşılan tarayıcıda açık değildi. Bilgisayar arayüzü üzerinden Chrome’u bağlama denemesi zaman aşımına uğradı. Panel URL’si de sağlanmadığından hosting üzerinde işlem yapılmadı.

Anahtar Plesk’te kaydedilip uygulama yeniden başlatılmadan yayın API’sindeki 503’ün düzelmesi beklenmez. Anahtar ayarlandıktan sonra tokensız `/api2/birimler` isteğinin `401` dönmesi normaldir; kayıtları görmek için geçerli `Admin` bearer token gerekir.

## Test kullanıcısı talebi ve sonuç

Kullanıcı, test için `stk_Kullanici` tablosunda `Admin` rolüyle, ilk etkin şubeye bağlı ve etkin kalacak bir hesap oluşturulmasına onay verdi. Bağlantı başarısız olduğu denemelerde insert çalıştırılmadı. Daha sonra `SELECT 1` başarılı olsa da test kullanıcısı oluşturma veya API login testi tekrar yapılmadı.

**Son durum:** Veritabanında test hesabı yoktur; Plesk ayarı yapılmamıştır; API2 kodu yerel browser testinde başarılı çalışmıştır.

## Çalışma alanı notu

Test için oluşturulan geçici yardımcı proje ve derleme çıktıları temizlendi. `appsettings.json` ve `deploy-manifest.json` daha önce çalışma ağacında değiştirilmiş görünüyordu; bu rapor çalışmasında içerikleri düzenlenmedi. Üretim anahtarı, kullanıcı parolası veya MySQL bağlantı bilgisi bu dosyaya yazılmadı.
