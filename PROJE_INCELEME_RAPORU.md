# GAMABEL MVC - Kapsamlı Proje İnceleme Raporu

**Tarih:** 12 Ekim 2026  
**Versiyon:** v3.4.2  
**Durum:** Aktif Geliştirme ve Üretim Kullanımı

---

## 📋 İçindekiler
1. [Kritik Sorunlar](#kritik-sorunlar)
2. [Yüksek Öncelikli Sorunlar](#yüksek-öncelikli-sorunlar)
3. [Orta Öncelikli Sorunlar](#orta-öncelikli-sorunlar)
4. [Düşük Öncelikli Sorunlar](#düşük-öncelikli-sorunlar)
5. [Öneriler](#öneriler)
6. [Proje Özeti](#proje-özeti)

---

## 🔴 Kritik Sorunlar

### 1. **GÜVENLİK: Veritabanı Kimlik Bilgileri Kaynak Kodunda Açık**

**Şiddet:** KRITIK  
**Dosya:** `appsettings.json`, `appsettings.Development.json`

```json
// appsettings.json
"ConnectionStrings": {
  "MyConnection": "Server=localhost;Database=u2636310_dbE97;User=u2636310_userE97;Password=VQLjJ1hmp3VdOQTp;SslMode=Preferred;"
}

// appsettings.Development.json
"ConnectionStrings": {
  "MyConnection": "Server=94.73.145.55;Database=u2636310_dbE97;User=u2636310_userE97;Password=VQLjJ1hmp3VdOQTp;SslMode=Preferred;"
}
```

**Sorun:**
- Veritabanı şifresi açık metin olarak kaynak kodda saklanıyor
- Gerçek IP adresi (94.73.145.55) ve veritabanı adı açık
- Git history'de kalıcı olarak saklanıyor
- Herkes tarafından erişilebilir durumda

**Çözüm:**
```bash
# 1. Şifreler Git history'den kaldırılmalı
git filter-branch --tree-filter 'rm -f appsettings.json appsettings.Development.json' HEAD

# 2. Ortam değişkenleri kullanılmalı
# Windows PowerShell:
$env:ConnectionStrings__MyConnection = "Server=...;Password=..."

# 3. User Secrets kullanılmalı (Development)
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MyConnection" "Server=...;Password=..."

# 4. Azure Key Vault / AWS Secrets Manager (Production)
```

---

### 2. **GÜVENLİK: SMTP Kimlik Bilgileri Açık**

**Şiddet:** KRITIK  
**Dosya:** `appsettings.json`

```json
"Smtp": {
  "Host": "mail.kurumsaleposta.com",
  "Port": 587,
  "Username": "info@gamabel.xyz",
  "Password": "JwOPuj6R1.-:-62w",
  "FromEmail": "info@gamabel.xyz",
  "FromName": "GAMABEL YATIRIM LTD"
}
```

**Sorun:**
- SMTP şifresi açık metin olarak saklanıyor
- E-posta hesabı ele geçirilme riski
- Spam/phishing saldırıları için kullanılabilir

**Çözüm:** Veritabanı şifresi gibi, ortam değişkenleri veya Key Vault kullanılmalı.

---

### 3. **GÜVENLİK: Infobip API Anahtarı Açık**

**Şiddet:** KRITIK  
**Dosya:** `appsettings.json`

```json
"Infobip": {
  "BaseUrl": "https://55z6pz.api.infobip.com",
  "WhatsAppSender": "447860088970",
  "WhatsAppRecipient": "905338582445",
  "WhatsAppTemplateName": "test_whatsapp_template_en",
  "WhatsAppTemplateLanguage": "en"
}
```

**Sorun:**
- API endpoint ve telefon numaraları açık
- WhatsApp API'ye yetkisiz erişim riski
- Mesaj gönderme maliyetleri artabilir

---

### 4. **GÜVENLİK: Düz Metin Şifre Depolaması**

**Şiddet:** KRITIK  
**Dosya:** `Controllers/AccountController.cs` (satır 56)

```csharp
await using var command = new MySqlCommand(
    "SELECT id, birim, rol, aktif_mi, email FROM admin_kullanicilar 
     WHERE kullanici_adi = @kullaniciAdi AND sifre = @sifre",
    connection);
```

**Sorun:**
- Şifreler veritabanında düz metin olarak saklanıyor
- Veritabanı sızıntısında tüm şifreler ele geçirilir
- Kimlik doğrulama güvenliği çok düşük

**Çözüm:**
```csharp
// BCrypt.Net-Next zaten yüklü, kullanılmalı
var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Sifre);
// Doğrulama:
bool isValid = BCrypt.Net.BCrypt.Verify(model.Sifre, storedHash);
```

---

### 5. **GÜVENLİK: Test Kullanıcısı Otomatik Oluşturuluyor**

**Şiddet:** KRITIK  
**Dosya:** `Program.cs` (satır 108-120)

```csharp
if (app.Environment.IsDevelopment())
{
    // ...
    var cmd = new MySqlCommand(
        "INSERT INTO admin_kullanicilar (kullanici_adi, sifre, birim, rol, aktif_mi) 
         VALUES ('test', 'test', 'AÇIK PAZAR', 'admin', TRUE)",
        conn);
}
```

**Sorun:**
- Test kullanıcısı (test/test) otomatik oluşturuluyor
- Admin yetkisiyle oluşturuluyor
- Production'a geçişte kaldırılması gerekiyor

**Çözüm:**
```csharp
if (app.Environment.IsDevelopment() && !app.Environment.IsProduction())
{
    // Test kullanıcısı sadece Development'ta
}
```

---

## 🟠 Yüksek Öncelikli Sorunlar

### 6. **Disk Alanı Sorunu: Derleme Çıktıları Çok Büyük**

**Şiddet:** YÜKSEK  
**Boyutlar:**
- `bin/` klasörü: **819.5 MB**
- `obj/` klasörü: **19.8 MB**
- `publish/` klasörü: **72.3 MB**
- **Toplam: ~911 MB**

**Sorun:**
- Geçici derleme dosyaları Git'e yüklenmemiş (iyi)
- Ancak `.gitignore` eksik girdiler var
- `bin/Debug/net9.0/temp/` klasörü iç içe geçmiş (çok derin)

**Çözüm:**
```bash
# .gitignore'a ekle
bin/
obj/
publish/
.emp/
*.tmp
*.temp

# Mevcut dosyaları temizle
dotnet clean
rm -r bin obj publish
```

---

### 7. **Git Durumu: Silinen Dosyalar Beklemede**

**Şiddet:** YÜKSEK  
**Dosya:** `.emp/build-validation/` klasörü

```
D .emp/build-validation/BCrypt.Net-Next.dll
D .emp/build-validation/BouncyCastle.Cryptography.dll
... (40+ dosya)
```

**Sorun:**
- 40+ dosya silinmiş ama Git'e commit edilmemiş
- Repository temiz değil
- Merge/rebase işlemleri sorunlu olabilir

**Çözüm:**
```bash
cd D:/GamabelMVC
git add -A
git commit -m "Derleme artefaktlarını kaldır"
git push
```

---

### 8. **Veritabanı Bağlantısı: Hardcoded Bağlantı Stringi**

**Şiddet:** YÜKSEK  
**Dosya:** `Services/DbConnectionFactory.cs`

```csharp
_connectionString = configuration.GetConnectionString("MyConnection") 
    ?? throw new InvalidOperationException("Connection string not configured");
```

**Sorun:**
- Bağlantı stringi `appsettings.json`'dan okunuyor (iyi)
- Ancak şifre açık metin (kritik sorun #1)
- Bağlantı havuzu yapılandırması yok

**Çözüm:**
```csharp
// Bağlantı havuzu ekle
var connectionString = configuration.GetConnectionString("MyConnection");
var builder = new MySqlConnectionStringBuilder(connectionString)
{
    Pooling = true,
    MaximumPoolSize = 100,
    MinimumPoolSize = 5,
    ConnectionTimeout = 30
};
```

---

### 9. **Kimlik Doğrulama: Session Tabanlı (Eski Yöntem)**

**Şiddet:** YÜKSEK  
**Dosya:** `Program.cs`, `Controllers/AccountController.cs`

```csharp
HttpContext.Session.SetInt32("PersonelId", personelId);
HttpContext.Session.SetString("KullaniciAdi", model.KullaniciAdi);
HttpContext.Session.SetString("Rol", roleName);
```

**Sorun:**
- Session tabanlı kimlik doğrulama eski yöntem
- CSRF saldırılarına açık
- Dağıtık sistemlerde sorunlu
- Token yenileme mekanizması yok

**Çözüm:**
```csharp
// JWT (JSON Web Token) kullanılmalı
// veya ASP.NET Core Identity
```

---

### 10. **Yetkilendirme: Rol Kontrolü Tutarsız**

**Şiddet:** YÜKSEK  
**Dosya:** `Program.cs` (satır 85-120)

```csharp
options.AddPolicy("PrsAdmin", policy =>
{
    policy.AddRequirements(new ModuleRequirement("Personel"));
    policy.AddRequirements(new RoleRequirement("admin"));  // küçük harf
});

options.AddPolicy("StsAdmin", policy =>
{
    policy.AddRequirements(new ModuleRequirement("STS"));
    policy.AddRequirements(new RoleRequirement("Admin"));  // büyük harf
});
```

**Sorun:**
- Rol adları tutarsız (admin vs Admin)
- Veritabanında hangi format kullanılıyor?
- Yetkilendirme hataları olabilir

**Çözüm:**
```csharp
// Tüm rolleri küçük harfle normalize et
var normalizedRole = roleName?.ToLowerInvariant() ?? "";
```

---

## 🟡 Orta Öncelikli Sorunlar

### 11. **Hata Yönetimi: Genel Exception Handling Eksik**

**Şiddet:** ORTA  
**Dosya:** `Controllers/PRS/HizliMesaiGirisiController.cs`

```csharp
catch (MySqlException ex)
{
    _logger.LogWarning($"Birimler sorgusu başarısız: {ex.Message}");
}
catch (Exception ex)
{
    _logger.LogError($"HizliMesaiGirisi Index Hatası: {ex.Message}");
    return BadRequest($"Hata: {ex.Message}");
}
```

**Sorun:**
- Hata mesajları kullanıcıya gösteriliyor
- Veritabanı yapısı hakkında bilgi sızıyor
- Güvenlik riski

**Çözüm:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Beklenmeyen hata");
    return BadRequest("İşlem başarısız oldu. Lütfen yönetici ile iletişime geçin.");
}
```

---

### 12. **Veritabanı: Migration Yönetimi Eksik**

**Şiddet:** ORTA  
**Dosya:** `Services/DbConnectionFactory.cs` (InitializeDatabaseAsync)

```csharp
// Migration'lar manuel SQL komutları olarak yazılıyor
string addColumnQuery = @"
    ALTER TABLE stk_Urun 
    ADD COLUMN IF NOT EXISTS Firma VARCHAR(255) NULL AFTER Birim;";
```

**Sorun:**
- Entity Framework Core migration sistemi kullanılmıyor
- Veritabanı şeması version kontrolü yok
- Rollback mekanizması yok
- Üretim ortamında riskli

**Çözüm:**
```bash
# EF Core migration'ları kullan
dotnet ef migrations add AddFirmaColumn
dotnet ef database update
```

---

### 13. **Logging: Yapılandırılmamış**

**Şiddet:** ORTA  
**Dosya:** `appsettings.json`

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

**Sorun:**
- Dosyaya yazma yapılandırması yok
- Üretimde loglar kaybolabilir
- Hata takibi zor

**Çözüm:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "File": {
      "Path": "logs/app-{Date}.log",
      "RollingInterval": "Day",
      "RollingFileSize": 104857600
    }
  }
}
```

---

### 14. **Performans: N+1 Sorgu Problemi**

**Şiddet:** ORTA  
**Dosya:** `Controllers/PRS/HizliMesaiGirisiController.cs` (satır 123-150)

```csharp
// Personel listesi alınıyor
using (var cmd = new MySqlCommand(personelQuery, conn))
{
    // Her personel için ayrı sorgu yapılabilir
}
```

**Sorun:**
- Veritabanı sorguları optimize edilmemiş
- Büyük veri setlerinde yavaş
- Bağlantı havuzu tükenebilir

**Çözüm:**
```csharp
// JOIN kullanarak tek sorguda al
var query = @"
    SELECT p.per_no, p.ad, p.soyad, p.per_statu, COUNT(m.id) as mesai_sayisi
    FROM personeller p
    LEFT JOIN mesai_kayitlari m ON p.per_no = m.per_no
    WHERE p.birim_adi = @birim
    GROUP BY p.per_no";
```

---

### 15. **Kod Kalitesi: Tekrarlayan Kod**

**Şiddet:** ORTA  
**Dosya:** Tüm Controller'lar

```csharp
// Her controller'da tekrarlanan kod
await using var connection = new MySqlConnection(_connectionString);
await connection.OpenAsync();

using (var cmd = new MySqlCommand(query, connection))
{
    cmd.Parameters.AddWithValue("@param", value);
    using (var reader = await cmd.ExecuteReaderAsync())
    {
        // ...
    }
}
```

**Sorun:**
- Tekrarlayan kod bakım zorluğu
- Hata riski yüksek
- DRY (Don't Repeat Yourself) ihlali

**Çözüm:**
```csharp
// Repository pattern kullan
public interface IPersonelRepository
{
    Task<List<Personel>> GetByBirimAsync(string birim);
    Task<Personel?> GetByIdAsync(string perNo);
}

public class PersonelRepository : IPersonelRepository
{
    // Tüm sorguları burada topla
}
```

---

## 🔵 Düşük Öncelikli Sorunlar

### 16. **Kod Stili: Tutarsız Adlandırma**

**Şiddet:** DÜŞÜK  
**Örnekler:**
- `KullaniciAdi` vs `kullaniciAdi`
- `Birim` vs `birim`
- `Rol` vs `rol`

**Çözüm:**
```csharp
// C# naming conventions'ı takip et
// PascalCase: public properties, methods
// camelCase: local variables, parameters
```

---

### 17. **Dokümantasyon: XML Yorumları Eksik**

**Şiddet:** DÜŞÜK  
**Dosya:** Tüm public methods

```csharp
// Yorum yok
public async Task<IActionResult> Index(int? yil = null, int? ay = null)
{
    // ...
}

// Olması gereken:
/// <summary>
/// Hızlı mesai girişi ana sayfasını gösterir
/// </summary>
/// <param name="yil">Yıl (varsayılan: cari yıl)</param>
/// <param name="ay">Ay (varsayılan: cari ay)</param>
public async Task<IActionResult> Index(int? yil = null, int? ay = null)
{
    // ...
}
```

---

### 18. **Testler: Unit Test Yok**

**Şiddet:** DÜŞÜK  
**Sorun:**
- Proje yapısında test projesi yok
- Kod değişiklikleri riskli
- Regresyon testi yapılamıyor

**Çözüm:**
```bash
dotnet new xunit -n GamabelMVC.Tests
dotnet add GamabelMVC.Tests reference GamabelMVC
```

---

### 19. **Bağımlılıklar: Eski Paketler**

**Şiddet:** DÜŞÜK  
**Dosya:** `gamabelmvc.csproj`

```xml
<PackageReference Include="FastReport.OpenSource.Export.PdfSimple" Version="2026.2.3" />
<PackageReference Include="FastReport.OpenSource.Web" Version="2026.2.3" />
```

**Sorun:**
- Paket versiyonları kontrol edilmeli
- Güvenlik güncellemeleri olabilir

**Çözüm:**
```bash
dotnet outdated
dotnet package update
```

---

### 20. **Yapılandırma: Ortam Değişkenleri Eksik**

**Şiddet:** DÜŞÜK  
**Dosya:** `Program.cs`

```csharp
// Ortam değişkenleri kullanılmıyor
var smtpHost = _configuration["Smtp:Host"];
```

**Çözüm:**
```bash
# .env dosyası oluştur
SMTP_HOST=mail.kurumsaleposta.com
SMTP_PORT=587
SMTP_USERNAME=info@gamabel.xyz
SMTP_PASSWORD=***

# Program.cs'de oku
var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
```

---

## 💡 Öneriler

### Acil Yapılması Gerekenler (1-2 Hafta)

1. **Şifreler ve API Anahtarlarını Kaldır**
   - Git history'den temizle
   - User Secrets / Key Vault kullan
   - Tüm şifreler değiştir

2. **Şifre Hashleme Ekle**
   - BCrypt.Net-Next kullan
   - Tüm mevcut şifreler hash'le
   - Veritabanı şemasını güncelle

3. **Git Repository'yi Temizle**
   - Silinen dosyaları commit et
   - `.gitignore` güncelle
   - `dotnet clean` çalıştır

### Kısa Vadede (1-2 Ay)

4. **JWT Kimlik Doğrulamasına Geç**
   - Session tabanlı sistemden çık
   - Token yenileme mekanizması ekle
   - CSRF koruması ekle

5. **Repository Pattern Uygula**
   - Tekrarlayan kodu kaldır
   - Veritabanı erişimini merkezi hale getir
   - Testleri kolaylaştır

6. **Entity Framework Core Kullan**
   - Manuel SQL'den çık
   - Migration sistemi ekle
   - LINQ sorguları kullan

### Orta Vadede (2-3 Ay)

7. **Unit Test Ekle**
   - xUnit/NUnit framework'ü kur
   - Kritik fonksiyonlar için test yaz
   - CI/CD pipeline'ında çalıştır

8. **Logging Sistemi Kur**
   - Serilog/NLog ekle
   - Dosyaya yazma yapılandır
   - Üretimde merkezi logging (ELK, Splunk)

9. **API Dokumentasyonu**
   - Swagger/OpenAPI ekle
   - Endpoint'leri dokümante et
   - Test arayüzü sağla

### Uzun Vadede (3-6 Ay)

10. **Microservices Mimarisi**
    - PRS ve STS'yi ayrı servislere böl
    - API Gateway ekle
    - Bağımsız deployment

---

## 📊 Proje Özeti

### Proje Bilgileri
- **Adı:** GAMABEL MVC
- **Versiyon:** v3.4.2
- **Framework:** ASP.NET Core 9.0
- **Dil:** C#
- **Veritabanı:** MySQL 8+
- **Dosya Sayısı:** 63 C# dosyası
- **Modüller:** 2 (PRS, STS)

### Teknoloji Stack
| Katman | Teknoloji |
|--------|-----------|
| Web Framework | ASP.NET Core MVC 9.0 |
| Veritabanı | MySQL 8+ |
| ORM | MySqlConnector (EF Core sınırlı) |
| Kimlik Doğrulama | Session (eski) |
| Raporlama | FastReport OpenSource 2026.2.3 |
| Excel | EPPlus 7.0.7, ClosedXML |
| E-posta | SMTP + arka plan kuyruk |
| WhatsApp | Infobip API |

### Proje Yapısı
```
Controllers/
├── PRS/          (Personel, Mesai, Puantaj, Ödeme)
├── STS/          (Sevkiyat, Eksik, Sipariş, Rapor)
├── Account       (Giriş/Kayıt)
└── Admin         (Sistem Yönetimi)

Models/
├── PRS/          (Personel, Mesai, Ödeme modelleri)
└── STS/          (Sevkiyat, Sipariş modelleri)

Services/
├── DbConnectionFactory
├── MailService
├── MailQueueService
├── OdemeTalimatService
├── SystemInfoService
└── InfobipWhatsAppMessageSender

Views/
├── PRS/          (Personel, Mesai, Puantaj, Ödeme sayfaları)
├── STS/          (Sevkiyat, Eksik, Sipariş, Rapor sayfaları)
├── Account/      (Giriş/Kayıt sayfaları)
└── Shared/       (Layout, ortak bileşenler)

Reports/         (FastReport .frx şablonları)
sql/             (Veritabanı şeması ve migration'lar)
wwwroot/         (CSS, JavaScript, FastReport varlıkları)
```

### Roller ve Yetkilendirme
**PRS Modülü:**
- `admin`: Tüm işlemler
- `birim_amiri`: Kendi birimi ile sınırlı

**STS Modülü:**
- `SubePersoneli`: Şube sevkiyatları
- `DepoSorumlusu`: Merkezi sevkiyat
- `Admin`: Tüm işlemler

### Disk Kullanımı
- `bin/`: 819.5 MB (derleme çıktıları)
- `obj/`: 19.8 MB (ara dosyalar)
- `publish/`: 72.3 MB (yayın dosyaları)
- **Toplam:** ~911 MB

---

## ✅ Kontrol Listesi

- [ ] Şifreler Git history'den kaldırıldı
- [ ] Ortam değişkenleri yapılandırıldı
- [ ] Şifre hashleme uygulandı
- [ ] JWT kimlik doğrulaması eklendi
- [ ] Repository pattern uygulandı
- [ ] Unit testler yazıldı
- [ ] Logging sistemi kuruldu
- [ ] API dokumentasyonu eklendi
- [ ] Performans optimizasyonları yapıldı
- [ ] Kod review tamamlandı

---

## 📞 İletişim

Sorular veya öneriler için proje yöneticisi ile iletişime geçin.

**Rapor Tarihi:** 12 Ekim 2026  
**Hazırlayan:** Kod İnceleme Aracı
