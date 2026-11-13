# Mobil Stok Sayım Uygulaması — Kurulum ve Entegrasyon Rehberi

Bu belge, mevcut ASP.NET Core uygulamasındaki mobil API'yi kullanarak Flutter
istemcisi geliştirecek kişi veya yapay zekâ için uygulama sözleşmesini, kurulum
adımlarını ve güvenlik kurallarını tanımlar.

## 1. Mimari ve güvenlik sınırları

Veri akışı yalnızca şu yönde olmalıdır:

```text
Flutter uygulaması
    └── HTTPS + JSON + Bearer JWT
          └── ASP.NET Core mobil API (/api)
                ├── Stok sayım verileri: StokSayimDbContext / EF Core
                └── Ürün ve kullanıcı verileri: mevcut MySQL veritabanı
```

**Mobil uygulama MySQL'e veya SQL Server'a doğrudan bağlanmaz.** Veritabanı adresi,
kullanıcı adı, parola ve bağlantı dizesi yalnızca sunucu yapılandırmasında
bulunur. Bu bilgileri Flutter koduna, `.env` dosyasına, APK'ya veya API
yanıtlarına eklemeyin. JWT imzalama anahtarı da yalnızca sunucuda kalır.

### API'nin sunucu tarafında kullandığı veritabanı tabloları

Mobil istemci bu tablolara erişmez. Tablo adları aşağıda yalnızca API'nin
sunucu tarafındaki veri akışını ve kurulum gereksinimlerini açıklamak içindir:

| Tablo | API'deki kullanım |
|---|---|
| `stk_Kullanici` | Login sırasında etkin STS kullanıcısı ve BCrypt parola hash'i doğrulanır. |
| `stok_sayim_kullanicilari` | Yalnızca `Admin` rolü için `Id`, `KullaniciAdi`, `AdSoyad`, `AktifMi` alanları okunur. |
| `stk_Urun` | `GET products` etkin ürünleri okur; `POST products` yeni ürün ekler. |
| `stok_sayimlar` | `GET counts` sayım oturumlarını listeler; diğer sayım işlemlerinde sayımın varlığı/durumu kontrol edilir. |
| `stok_sayim_sistem_stok` | `GET counts/{id}/system-stock` sistem stoklarını okur; hareketin sayımda yeni ürün olup olmadığı belirlenir. |
| `stok_sayim_hareketleri` | Hareketler okunur ve yeni hareketler yazılır. `MobileRequestId` UUID sütunu tekrar gönderimleri engeller. |

API, bu tablolara sunucudaki `ConnectionStrings:MyConnection` ayarı üzerinden
bağlanır. Flutter tarafına tablo şeması, DB erişim bilgisi veya SQL sorgusu
eklemeyin. UUID sütunu için gerekli migration ve uygulama adımı
[sunucu kurulum bölümünde](#32-veritabanı-migrasyonu) açıklanmıştır.

Üretimde HTTPS zorunludur. Sertifika doğrulamasını kapatmayın ve Android'de
güvensiz HTTP trafiğini açarak TLS hatalarını aşmayın. Geliştirme/test için de
mobil cihazın güvenebildiği bir HTTPS adresi kullanın.

## 2. Uygulama ve API adresini yapılandırma

API, mevcut ASP.NET Core web uygulamasının parçasıdır; ayrı bir API sunucusu
veya mobil uygulama için veritabanı hesabı gerekmez. Flutter'da API adresini
tek bir yapılandırma değeri olarak tutun:

```text
API_BASE_URL=https://gamabel.xyz/api/
```

Adresin sonunda `/` bulunmalıdır. İstek yollarını `auth/login`, `counts` gibi
göreli oluşturun; başında `/` olan yol, `/api/` önekini düşürebilir.

Flutter APK'sını üretim için şu şekilde derleyin:

```powershell
flutter build apk --dart-define=API_BASE_URL=https://gamabel.xyz/api/
```

Uygulama başlangıcında `API_BASE_URL` tanımlı değilse sessizce başka bir
sunucuya veya sahte veriye geçmeyin; açık ve anlaşılır bir yapılandırma hatası
gösterin. API adresi gizli bilgi değildir; JWT veya veritabanı anahtarı değildir.

## 3. Sunucu kurulum ve yayınlama

### 3.1 Sunucu sırları

ASP.NET Core aşağıdaki yapılandırmaları **sunucu ortamından veya gizli bilgi
yöneticisinden** almalıdır:

| Yapılandırma | Amaç | Nerede bulunur? |
|---|---|---|
| `ConnectionStrings__MyConnection` | Web uygulamasının kullandığı MySQL bağlantısı | Sunucunun güvenli .NET yapılandırması |
| `MobileApi__Jwt__SigningKey` | JWT imzalama anahtarı; en az 32 UTF-8 baytı | Sunucunun secret store / servis ortamı |

`ConnectionStrings__MyConnection`, uygulamanın `ConnectionStrings:MyConnection`
değerini ortam değişkeni ile geçersiz kılar. Gerçek bağlantı dizesini mobil
ekibe veya Flutter uygulamasına kopyalamayın.

Rastgele JWT anahtarını sunucuda oluşturun ve güvenli biçimde saklayın. Örnek
PowerShell (yerel geliştirme için):

```powershell
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 64
$rng.GetBytes($bytes)
$env:MobileApi__Jwt__SigningKey = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet run
```

Her üretim ortamı için anahtar, dağıtım secret store'unda saklanmalı; kaynak
koda veya `appsettings*.json` dosyasına eklenmemelidir. Anahtar değişirse önceki
JWT'ler geçersiz olur ve kullanıcıların yeniden giriş yapması gerekir.

### 3.2 Veritabanı migrasyonu

Hareket tekrarlarını önlemek için nullable UUID sütunu ve benzersiz indeks
eklenmiştir. Dağıtımdan önce veritabanı yedeği alın, bağlantı dizesinin doğru
ortama işaret ettiğini doğrulayın ve migrasyonu yayın/DB yöneticisi sürecinizle
uygulayın:

```powershell
dotnet ef database update --context StokSayimDbContext
```

İlgili migration: [20261113062452_MobileApiMovementIdempotency.cs](./Data/Migrations/20261113062452_MobileApiMovementIdempotency.cs).
Uygulama başlangıcında migration otomatik çalıştırılmaz. Mevcut web üzerinden
oluşturulmuş hareketlerde UUID `null` kalabilir.

### 3.3 API yayınlandıktan sonra doğrulama

* OpenAPI belgesi: `https://gamabel.xyz/openapi/v1.json`
* API hazır olma kontrolü: `https://gamabel.xyz/api/health`
* Örnek istekler: [mobile-api.http](./mobile-api.http)
* ASP.NET Core mobil uç noktaları:
  [MobileAuthController.cs](./Controllers/MobileApi/MobileAuthController.cs),
  [MobileInventoryController.cs](./Controllers/MobileApi/MobileInventoryController.cs)
* İstek/yanıt tipleri:
  [MobileApiContracts.cs](./Api/MobileApiContracts.cs)

OpenAPI belgesi `wwwroot/openapi/v1.json` dosyasından sunulur. Dağıtım
paketinde bu dosyanın bulunduğunu doğrulayın.

`GET /api/health`, yalnızca JWT imzalama anahtarı geçerli, veritabanı
erişilebilir ve uygulanmamış EF Core migration'ı yoksa `200 {"status":"healthy"}`
döndürür. Sorun varsa `503 {"status":"unhealthy"}` döner; bağlantı dizesi,
anahtar veya istisna ayrıntısı açıklanmaz. Tam hata için uygulamanın sunucu
loglarını inceleyin. Bu uç nokta mobil istemcinin veri uç noktası değildir.

JWT anahtarı eksik veya kısa ise `/api` uç noktaları DB'ye gitmeden
`503 Service Unavailable` döndürür ve sunucu loguna güvenli bir yapılandırma
hatası yazar. Anahtar düzeltildiğinde tokensız korumalı uç noktalar `401`
döndürmelidir. Böylece yanlış sunucu yapılandırması, geçersiz kullanıcı token'ı
gibi görünmez.

Login uç noktası istemci IP'si başına dakikada en fazla 5 isteğe izin verir.
Aşımda `429 Too Many Requests` ve genel Problem Details yanıtı gelir. Nginx
önünde çalışan sunucuda `UseForwardedHeaders` yalnızca ASP.NET Core'un güvenilir
varsayılan proxy/ağlarından gelen `X-Forwarded-For` değerlerini kullanır;
başka makinedeki reverse proxy kullanılıyorsa o proxy ayrıca güvenilir proxy
olarak `MobileApi__TrustedProxies__0=<sabit özel IP>` ayarıyla
yapılandırılmalıdır. Same-host Nginx için loopback adresleri varsayılan olarak
güvenilirdir. Docker'daki reverse proxy için sabit özel IP kullanın; tüm
proxy'lere güvenecek şekilde forwarded-header güvenlik listesini kapatmayın.
İstemciden gelen proxy başlıklarına doğrudan güvenmeyin.

### 3.4 Gerçek site ve APK için yayın kontrol listesi

Flutter birim testlerinin ve `dart analyze` komutunun başarılı olması gerçek
sitede API erişimini doğrulamaz. Gerçek giriş ve eşitleme için önce:

1. ASP.NET Core sunucusunda `ConnectionStrings__MyConnection` ve en az 32
   UTF-8 baytlık `MobileApi__Jwt__SigningKey` ayarlarının üretim ortamında
   tanımlı olduğunu doğrulayın. Gerçek değerleri mobil uygulamaya, kaynak
   koda veya loglara koymayın. Linux systemd örneği için
   [Deployment Rehberi](./DEPLOYMENT_GUIDE.md#3-systemd-service) bölümüne bakın.
2. `GET https://gamabel.xyz/api/health` isteğinin `200` ve
   `{"status":"healthy"}` döndürdüğünü doğrulayın. `503` gelirse API
   uç noktalarını mobil istemcide tekrar denemeden önce sunucu yapılandırmasını,
   DB bağlantısını ve bekleyen migration'ları düzeltin.
3. `MobileApiMovementIdempotency` migration'ını doğru uzak veritabanına
   uygulayın; health endpoint bu ve diğer bekleyen EF migration'larını
   `503` olarak bildirir.
4. API'nin HTTPS üzerinden yayımlandığını kontrol edin. Flutter derleme
   adresinin sonu `/api/` olmalı; örneğin
   `https://gamabel.xyz/api/`. `http://` adresi veya eksik `/api/` öneki
   kullanmayın.
5. `https://gamabel.xyz/openapi/v1.json` belgesinin gerçek siteden
   erişilebildiğini kontrol edin.
6. Tokensız `GET /api/counts` ve `GET /api/products` isteklerinin `401`
   döndüğünü doğrulayın. `503` gelirse API yapılandırmasını ve health endpoint'i;
   `500` gelirse istek zamanındaki sunucu loglarını inceleyin. OpenAPI `200`
   dönmesi API'nin hazır olduğunu kanıtlamaz.
7. Login'i `stk_Kullanici` tablosunda etkin ve BCrypt hash'li parolası bulunan
   bir STS hesabıyla deneyin. Kullanıcı adı/parola mobil uygulamada sabit
   kodlanmamalıdır. Dakikada 5'ten fazla login denemesi `429` ile
   sınırlandırılır.
8. Flutter APK'sını gerçek API adresiyle yeniden derleyin:

   ```powershell
   flutter build apk --dart-define=API_BASE_URL=https://gamabel.xyz/api/
   ```

   `API_BASE_URL` derleme sırasında verilmezse
   `String.fromEnvironment('API_BASE_URL')` boş değer döndürür; uygulama
   başlangıçta yapılandırma hatası göstermelidir. Eski bir APK, son istemci
   değişikliklerini veya gerçek API adresini içermeyebilir; yeni APK'yı
   kurduğunuzu doğrulayın.
9. Önce test ortamında, ardından kontrollü gerçek verilerle sırasıyla login,
   `GET counts`, `GET products`, bir sayımın sistem stoğunu görüntüleme ve
   hareket eşitleme akışlarını doğrulayın. Aynı UUID ve aynı gövdeyle hareketi
   tekrar göndermek ilk istekte `201`, başarılı tekrarda `200` vermelidir.
   Üretimde hareket oluşturma testini yalnızca uygun test sayımı/verisiyle yapın.

Sunucu API sözleşmesinde hareketi iptal eden veya silen bir uç nokta yoktur.
İstemci, gönderilmiş hareketin sunucuda iptal edilebileceğini varsaymamalı ve
iptal/silme düğmesini sunucu işlemi varmış gibi göstermemelidir. Çevrimdışı
kuyrukta henüz gönderilmemiş bir öğeyi yerel olarak kaldırmak, sunucudaki
hareketi iptal etmekten farklıdır.

## 4. Login ve token kullanımı

### 4.1 Mobil kullanıcı hesabı

`POST /api/auth/login`, mevcut STS kullanıcı tablosundaki etkin kullanıcıyı
doğrular. Kullanıcı adı/parola mobil uygulamada sabit kodlanmaz; kullanıcıdan
alınır. Hesap, yönetici tarafından mevcut kullanıcı yönetimi üzerinden
oluşturulur/etkinleştirilir. Parola unutulursa eski parolayı veritabanından
okumaya çalışmayın; yönetici mevcut parola sıfırlama sürecini kullanmalıdır.

API yalnızca `stk_Kullanici.AktifMi` etkin olan ve `SifreHash` alanında BCrypt
hash'i bulunan hesaplarla mobil girişe izin verir. Eski düz metin parola
kayıtlarıyla giriş yapılmaz; bu hesaplar güvenli parola sıfırlama/geçiş
sürecinden geçirilmelidir. API hiçbir yanıtta parola veya hash döndürmez.

Login isteği:

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "depo-kullanici",
  "password": "kullanicinin-parolasi"
}
```

Başarılı yanıt (`200 OK`):

```json
{
  "accessToken": "<JWT>",
  "tokenType": "Bearer",
  "expiresAt": "2026-11-13T09:00:00+00:00",
  "user": {
    "id": 12,
    "username": "depo-kullanici",
    "fullName": "Örnek Kullanıcı",
    "role": "DepoSorumlusu"
  }
}
```

Token ömrü 30 dakikadır; ayrıca refresh-token uç noktası yoktur. Süre
doldurulduğunda login ekranına dönün veya kullanıcı deneyiminize uygun yeniden
giriş akışı başlatın. Kullanıcı adı/parolayı veya token'ı loglamayın.

### 4.2 Flutter'da token saklama ve gönderme

Yapay zekâ/uygulama geliştiricisi:

1. Depoda mevcut `ApiService`, HTTP istemcisi ve güvenli saklama çözümü varsa
   önce bunları inceleyip yeniden kullanmalıdır.
2. Token'ı `shared_preferences` gibi düz tercih deposunda değil, projede
   mevcutsa güvenli depoda; yoksa platform güvenli deposu sağlayan uygun bir
   paketle (ör. `flutter_secure_storage`) saklamalıdır.
3. Login dışındaki tüm isteklerde aşağıdaki başlığı göndermelidir:

   ```http
   Authorization: Bearer <accessToken>
   Content-Type: application/json
   ```

4. Çıkışta saklanan token'ı silmelidir. Token süresi dolarsa API `401` döndürür;
   aynı isteği sonsuz döngüyle tekrar denemek yerine kullanıcıdan yeniden giriş
   istemelidir.
5. POST isteğini ağ zaman aşımı nedeniyle tekrar gönderirken yalnızca hareket
   için aynı UUID'yi korumalıdır. Yeni bir hareket için yeni UUID üretmelidir.

## 5. API kökü ve uç noktalar

Üretim kökü `https://gamabel.xyz/api/` şeklindedir. Aşağıdaki yollar bu
köke eklenir. Login haricindeki tüm uç noktalar JWT ister.

| Yöntem | Yol | Kimlik doğrulama | Açıklama |
|---|---|---|---|
| `POST` | `auth/login` | Hayır | STS kullanıcısını doğrular, JWT döndürür |
| `GET` | `users` | Evet — yalnızca `Admin` | Kullanıcı dizinini güvenli alanlarla listeler |
| `GET` | `counts` | Evet | Sayım oturumlarını listeler |
| `GET` | `products` | Evet | Etkin ürünleri listeler |
| `POST` | `products` | Evet | Ürün kataloğuna ürün ekler |
| `GET` | `counts/{id}/system-stock` | Evet | Sayımın sistem stoklarını getirir |
| `GET` | `counts/{id}/movements` | Evet | İptal edilmemiş hareketleri getirir |
| `POST` | `counts/{id}/movements` | Evet | Sayıma hareket ekler |

Tarih/saat değerleri UTC ISO 8601 olarak gönderilir. JSON alanları
`camelCase`'tir. Liste uç noktaları JSON dizisi döndürür.

### 5.1 Yönetici kullanıcı dizini — `GET users`

Uç nokta yalnızca `Admin` rolüne sahip, geçerli bearer token ile çağrılabilir.
Login yine `stk_Kullanici` tablosundan yapılır; kullanıcı dizini ise aynı
sunucu bağlantısını kullanarak `stok_sayim_kullanicilari` tablosundan okunur.
Sorgu yalnızca aşağıdaki dört sütunu seçer; parola/hash bilgisi döndürülmez.

```json
[
  {
    "id": 12,
    "username": "ornek.kullanici",
    "fullName": "Örnek Kullanıcı",
    "isActive": true
  }
]
```

Admin dışındaki geçerli token'lar `403 Forbidden`, token bulunmaması veya
geçersiz olması `401 Unauthorized` döndürür. Liste mobil uygulamada yönetici
oturumu açıldıktan sonra otomatik yüklenir.

### 5.2 Sayımlar — `GET counts`

Örnek kayıt:

```json
{
  "id": 1,
  "name": "Kasım sayımı",
  "status": "Açık",
  "openedAt": "2026-11-13T06:00:00+00:00",
  "closedAt": null,
  "createdBy": "depo-kullanici"
}
```

### 5.3 Ürünler — `GET products`, `POST products`

`GET products`, etkin ürünleri döndürür. Ürün oluşturma isteği:

```json
{
  "name": "Örnek ürün",
  "barcode": "8690000000000",
  "unit": "Adet",
  "company": null
}
```

`name` zorunlu (en fazla 200 karakter); `barcode` en fazla 50, `unit` en fazla
20, `company` en fazla 255 karakterdir. `unit` boş bırakılırsa `Adet` kullanılır;
barcode ve company boş/null olabilir. Yanıt:

```json
{
  "id": 27,
  "name": "Örnek ürün",
  "barcode": "8690000000000",
  "unit": "Adet",
  "company": null,
  "isActive": true
}
```

Barcode veritabanında benzersizdir; aynı barcode ile ekleme `409 Conflict`
döndürür. Barcode göndermeden oluşturulan bir ürün daha sonra barkod ile
tanımlanabilir.

### 5.4 Sistem stoğu — `GET counts/{id}/system-stock`

Örnek kayıt:

```json
{
  "id": 41,
  "countId": 1,
  "stockCode": null,
  "referenceCode": null,
  "barcode": "8690000000000",
  "productName": "Örnek ürün",
  "incomingQuantity": null,
  "outgoingQuantity": null,
  "systemStock": 3,
  "unit": "Adet",
  "purchasePrice": null,
  "salesPrice": null,
  "pieceType": null
}
```

Opsiyonel stok/fiyat alanları `null` olabilir. Sayım yoksa `404` döner; sayım
var fakat sistem stoğu yoksa boş dizi `[]` döner.

### 5.5 Hareketler — `GET/POST counts/{id}/movements`

GET sonucu iptal edilmemiş hareket dizisidir. Yeni hareket isteği:

```json
{
  "uuid": "ad080953-2ee4-4eca-94de-d2751acd987b",
  "barcode": "8690000000000",
  "productName": "Örnek ürün",
  "quantity": 2,
  "comment": null
}
```

`uuid` standart 36 karakterli UUID olmalı; `barcode` en fazla 100, `productName`
en fazla 300 karakter, `comment` en fazla 2000 karakterdir. `quantity` pozitif
olmalı ve en az `0.001` olmalıdır. Kullanıcı adı istek gövdesinden alınmaz;
doğrulanmış token'dan kaydedilir. Yalnızca açık sayımlara hareket eklenebilir.

Oluşturulan hareket:

```json
{
  "id": 88,
  "countId": 1,
  "uuid": "ad080953-2ee4-4eca-94de-d2751acd987b",
  "barcode": "8690000000000",
  "productName": "Örnek ürün",
  "quantity": 2,
  "username": "depo-kullanici",
  "occurredAt": "2026-11-13T06:30:00+00:00",
  "comment": null,
  "source": "Mobil",
  "isNewProduct": false
}
```

* İlk kayıt `201 Created` döndürür.
* Aynı UUID ve aynı sayım/içerik tekrar gönderilirse önceki kayıt `200 OK` ile
  döner; ikinci hareket oluşturulmaz.
* Daha önce başka içerikle kullanılmış UUID `409 Conflict` döndürür.
* UUID her **yeni** hareket için bir kez üretilir; ağ hatasında aynı hareketi
  tekrar denemek için UUID ve içerik korunur.

## 6. Hatalar ve mobil istemci davranışı

Hatalar `application/problem+json` biçimindedir. Örnek:

```json
{
  "title": "Count is closed",
  "status": 409,
  "detail": "Movements cannot be added to a closed stock-count session."
}
```

| HTTP durumu | Anlamı | İstemci davranışı |
|---|---|---|
| `400` | Eksik/geçersiz JSON, UUID veya alan değeri | Alan doğrulama mesajı göster |
| `401` | Login bilgileri yanlış veya token eksik/geçersiz/süresi dolmuş | Login iste; sonsuz otomatik retry yapma |
| `403` | Yetki reddedildi | İşlemi durdur ve anlaşılır mesaj göster |
| `404` | Sayım bulunamadı | Eski/geçersiz sayım seçimini yenile |
| `409` | Barcode çakışması, kapalı sayım veya UUID/içerik çakışması | Duruma göre kullanıcıya bildir; UUID çakışmasında yeni UUID'yi körlemesine üretme |
| `500` | Sunucu hatası | Genel hata göster, teknik ayrıntı/connection string'i kullanıcıya yazma |

Uygulama ağ yokluğu ve zaman aşımını API hata kodlarından ayrı ele almalıdır.
Yanıt gövdesinde parola, hash, bağlantı dizesi veya JWT imza anahtarı bulunmaz.

## 7. Kaynak dosyalar

* [API sözleşmeleri ve doğrulama kuralları](./Api/MobileApiContracts.cs)
* [Login ve JWT üretimi](./Services/MobileApi/MobileAuthService.cs)
* [Ürün/sayım/hareket veri erişimi](./Services/MobileApi/MobileInventoryService.cs)
* [Sunucu yapılandırması ve kimlik doğrulama kaydı](./Program.cs)
* [Hareket UUID migration'ı](./Data/Migrations/20261113062452_MobileApiMovementIdempotency.cs)
* [OpenAPI 3.0 şeması](./wwwroot/openapi/v1.json)
* [HTTP istek örnekleri](./mobile-api.http)

## 8. Mobil uygulamayı hazırlayacak yapay zekâ için görev kuralları

Bu bölüm, Flutter istemcisini hazırlayacak yapay zekânın uygulaması gereken
talimatlardır:

1. Depoda Flutter istemcisi varsa önce `ApiService`, modeller, mevcut HTTP
   istemcisi ve token saklama çözümünü bul; mevcut yapıyı yeniden kullan ve API
   sözleşmesini değiştirmeden entegre et.
2. Flutter API adresi `API_BASE_URL` derleme tanımından gelir. Bu değeri sunucu
   adresini uygulama sahibinden/dağıtım ayarından alarak ver; veritabanı
   yapılandırmasından türetmeye çalışma. URL `/api/` ile bitmelidir.
3. Kullanıcı adı/parola mobil giriş ekranından alınır. Kullanıcı bilgileri
   mevcut STS kullanıcısına ait olmalıdır. `MobileApi__Jwt__SigningKey` ve
   `ConnectionStrings__MyConnection` sunucu ayarlarıdır; mobil uygulamada
   aranmaz, okunmaz veya saklanmaz.
4. Verileri yalnızca HTTPS API'sinden al. İstek/yanıt modellerini bu belgedeki
   `camelCase` JSON sözleşmesine göre oluştur; tarihleri ISO 8601 olarak parse
   et ve `null` olabilen alanları zorunlu varsayma.
5. Login başarılı olduğunda `accessToken` değerini güvenli platform deposunda
   sakla; parola veya token'ı loglama. `counts`, `products`, `system-stock` ve
   `movements` isteklerine `Authorization: Bearer <token>` ekle. Login isteğine
   token ekleme; çıkışta token'ı sil.
6. Yeni katalog ürünü için `POST products`, sayım hareketi için
   `POST counts/{id}/movements` çağır. Kullanıcı adını hareket gövdesine koyma;
   sunucu kullanıcıyı JWT'den alır.
7. Hareket başına bir UUID oluştur. Ağ hatası nedeniyle aynı hareketi yeniden
   gönderirken hem aynı UUID'yi hem de aynı içeriği koru. Yeni UUID ile tekrar
   gönderme kopya hareket doğurabilir.
8. `400/401/403/404/409/500`, bağlantı kesintisi ve zaman aşımını ayrı ele al.
   `401` sonrası sonsuz tekrar yapma; `409` yanıtını kullanıcıya uygun biçimde
   bildir. Sunucu hata ayrıntılarını veya bağlantı bilgilerini ekranda gösterme.
9. SQL sürücüsü, MySQL/SQL Server bağlantı dizesi, veritabanı kullanıcı hesabı
   veya JWT imza anahtarı mobil bağımlılıklarına ekleme. TLS/sertifika
   doğrulamasını devre dışı bırakma.
10. Entegrasyonu login, yetkili GET, ürün oluşturma, sistem stoğu yükleme,
    hareket oluşturma ve aynı UUID ile güvenli tekrar senaryolarıyla doğrula.

Örnek bağlantı düzeni (projede mevcut HTTP ve güvenli depolama paketleri varsa
onları kullan; yoksa projeye uygun paketleri ekle):

```dart
const apiBaseUrl = String.fromEnvironment('API_BASE_URL');

Uri apiUri(String path) {
  if (apiBaseUrl.isEmpty || !apiBaseUrl.endsWith('/')) {
    throw StateError('API_BASE_URL must be set and end with /');
  }
  return Uri.parse(apiBaseUrl).resolve(path);
}

// Login: POST apiUri('auth/login'), JSON body: username + password.
// On 200, securely store response.accessToken; never store the password.

// Authenticated request: GET apiUri('counts')
// Header: Authorization: Bearer <securely-stored accessToken>
```

Mobil uygulama hiçbir aşamada MySQL/SQL Server'a bağlanmaz: kimlik doğrulama,
yetkilendirme, veri doğrulama ve veritabanı işlemlerinin tamamı ASP.NET Core
API'sinde yapılır.
