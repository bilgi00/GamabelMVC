## 📧 Ödeme Talimatı Mail Gönderme — Dokümantasyon ve İyileştirme Önerileri

Bu doküman, projeye eklenen mail gönderim bileşenleri (Models/PRS/MailModels.cs, Services/MailService.cs, Services/MailQueueService.cs, Controllers/PRS/MailGonderController.cs, Views/MailGonder/*) temelinde hızlı bir özet, tespit edilen eksiklikler ve geliştirme önerileri içerir.

**Durum:** Dosyalar repoya eklendi ve temel iş akışı (talimat seç → firmaları seç → kuyruğa al → arka planda gönder) uygulanmış.

----------------

**Kısa Özet (Adımlar 1–8)**
- Veritabanı: `prs_ot_firmalar` için `email` sütunu eklendi ve mail log/kuyruk tabloları önerildi.
- Modeller: `Models/PRS/MailModels.cs` eklendi (mail/queue modelleri olmalı).
- Servisler: `MailService` (SMTP ile gönderim) ve `MailQueueService` (arka plan HostedService) eklendi.
- Controller: `Controllers/PRS/MailGonderController.cs` eklendi; POST `/MailGonder/Gonder` ve `/TestGonder` endpoint'leri sağlanmış.
- View: `Views/MailGonder/Index.cshtml` ve `Rapor.cshtml` eklendi.
- Program.cs: servislerin kaydı yapılmalı (`AddScoped<IMailService,MailService>()` ve `AddHostedService<MailQueueService>()`).
- Firmaların email adresleri güncellendi (örnek SQL verildi).

----------------

Bulunan Eksikler / Düzeltme Önerileri

- Güvenlik / Konfigürasyon
  - SMTP şifresini `appsettings.json` içinde düz metin tutmayın. Kullanıcı-secrets veya ortam değişkenleri (`Environment.GetEnvironmentVariable`) kullanın.
  - SMTP için `MailKit` kullanın (`MailKit` + `MimeKit`). `SmtpClient` eski/engelleyici olabilir.

- Veritabanı ve Transaction
  - Mail kuyruğa alırken ve `mail_gonderim_log` yazarken tek bir transaction kullanın (kuyruğa ekle + log atomik olmalı).
  - Önerilen kuyruk tablosu: `mail_queue` (Id, TalimatId, FirmaId, To, Cc, Bcc, Subject, Body, Status, Attempts, LastAttempt, CreatedAt, SentAt, Error).

- Hata Yönetimi ve Retry
  - `MailQueueService` içinde gönderimde `Polly` ile exponential backoff retry mekanizması ekleyin.
  - Başarısız gönderimlerde `Attempts` artırılsın; belirli sayı aşıldığında `Başarısız` olarak işaretlensin ve alert üretin.

- Performans ve Ölçek
  - Büyük hacimlerde toplu gönderim için paralel ama kontrollü worker (degreeOfParallelism) kullanın.
  - Gönderim hızı limitleri (rate-limiting) uygulayın veya SMTP sağlayıcısı ile anlaşmaya göre throttle edin.

- İzlenebilirlik
  - `mail_gonderim_log` tablosuna `KullaniciId`, `KullaniciAdi`, `IpAddress`, `UserAgent` eklenmeli (zaten kısmen mevcutsa kontrol edin).
  - Gönderim metrikleri için Prometheus/Telemetry entegrasyonu düşünün (success/fail count, latency).

- Email İçerik ve Şablonlar
  - HTML şablonları veri ile güvenli şekilde birleştirilirken HTML-injection ve XSS önlemleri alın.
  - Şablon değişkenleri sanitize edilsin; büyük tablolar için `TABLE_HTML` oluşturulmadan önce sanitize edin.
  - Şablon önizlemesi (Admin) ekleyin.

- Idempotency ve Duplicate Prevention
  - Aynı talimat/firma çifti için ikinci gönderim istem dışı tekrar göndermesin: kuyruğa ekleme öncesi dedup kontrolü veya idempotency key kullanın.

- Test ve Local Dev
  - Geliştirme ortamında `smtp4dev` veya `MailHog` kullanarak entegre testler yazın.
  - Unit test: `MailService` için SMTP bağlantı hatası senaryoları, `MailQueueService` için retry senaryoları.

----------------

Önerilen Veritabanı SQL (mail_queue örneği)

```sql
CREATE TABLE IF NOT EXISTS mail_queue (
  id INT PRIMARY KEY AUTO_INCREMENT,
  talimat_id INT NULL,
  firma_id INT NULL,
  alici_email VARCHAR(255) NOT NULL,
  cc VARCHAR(1000) NULL,
  bcc VARCHAR(1000) NULL,
  konu VARCHAR(255) NOT NULL,
  icerik MEDIUMTEXT,
  durum ENUM('Beklemede','Isleniyor','Gonderildi','Basarisiz') DEFAULT 'Beklemede',
  attempts INT DEFAULT 0,
  last_attempt DATETIME NULL,
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
  sent_at DATETIME NULL,
  error TEXT NULL,
  INDEX idx_talimat (talimat_id),
  INDEX idx_firma (firma_id),
  INDEX idx_durum (durum)
);
```

Hızlı Firma Email Güncelleme (örnek)

```sql
UPDATE prs_ot_firmalar SET email = 'abc@firma.com' WHERE id = 12;
```

----------------

Program.cs Örneği (servis kayıtları)

```csharp
// Program.cs
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddSingleton<IEmailTemplateRenderer, RazorTemplateRenderer>();
builder.Services.AddHostedService<MailQueueService>();

// SMTP credential: from config via IOptions<SmtpOptions>
```

Geliştirme Öncelikleri (kısa)
- 1) Güvenli konfigürasyon (secrets/env) — kritik
- 2) Mail kuyruğu tablosu + transaction — yüksek
- 3) Retry + izleme (Polly + logging) — yüksek
- 4) Şablon sanitizasyonu + preview — orta
- 5) Integration tests (smtp4dev) — orta

----------------

Kontrol Listesi — Uygulanacak Hızlı Düzeltmeler
- `appsettings.json` içindeki SMTP şifresini kaldırın; `dotnet user-secrets set` veya env var kullanın.
- `MailService` içinde `using` blokları, `try/catch` ve log ekleyin.
- `MailQueueService` gönderim işini paralel değil kontrollü worker ile çalıştırsın.
- `mail_queue` tablosuna `attempts` ve `last_attempt` ekleyin; `Basarisiz` için alert atın.

Sonuç ve Next Steps
- Ben bu dosyayı güncelledim; isterseniz şu adımları atabilirim:
  1) `mail_queue` ve `mail_gonderim_log` için SQL script oluşturup repoya ekleyebilirim.
  2) `Program.cs` içine örnek servis kayıtlarını ekleyebilirim.
  3) `MailService` için MailKit tabanlı örnek implementasyon hazırlayıp bir PR açabilirim.

Hangi adımı önce yapmamı istersiniz? (1/2/3 veya başka bir öneri belirtin.)
