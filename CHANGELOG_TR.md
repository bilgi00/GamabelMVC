# 📋 CHANGELOG - GAMABEL MVC

**Format**: [VERSION] - [DATE] - [STATUS]  
**Son Güncelleme**: 10 Temmuz 2026

---

## v3.4.1 - 10 Temmuz 2026 - ✅ HOTFIX

### 🖨️ Ek Mesai PDF Dışa Aktarım Düzeltmeleri

- **✅ PDF (Birim) ve PDF (Personel) butonları çalışır hale getirildi**:
  - `Views/PRS/Mesai/Index.cshtml` içinde buton akışı yeniden düzenlendi
  - `type="button"` kullanılarak istenmeyen form submit davranışı engellendi
  - `printBirimPdf` ve `printPersonelPdf` fonksiyonları ile tıklama akışı sadeleştirildi

- **✅ Profesyonel PDF/Yazdırma yerleşimleri eklendi**:
  - Birim bazlı özet tablo düzeni
  - Personel bazlı gruplu tablo düzeni ve alt toplamlar
  - İmza alanları, oluşturulma zamanı ve kurumsal başlık eklendi

- **✅ Tarayıcı uyumluluğu için yazdırma akışı güçlendirildi**:
  - Popup tabanlı yaklaşım yerine `window.print()` tabanlı tam ekran yazdırma konteyneri kullanıldı
  - Yazdırma sonrası geçici DOM temizliği otomatik hale getirildi
  - Boş veri durumunda `Yazdırılacak veri yok` uyarısı eklendi

- **✅ Kayıt sonrası giriş engeli kaldırıldı**:
  - `Controllers/AccountController.cs` içinde yeni kullanıcı kaydı sırasında `aktif_mi = 1` atanıyor
  - Yeni oluşturulan kullanıcılar ek manuel aktivasyon gerektirmeden giriş yapabiliyor

- **✅ Hata giderme ve doğrulama**:
  - Razor içinde literal `@media` kullanımı `@@media` olarak düzeltildi
  - PDF fonksiyonları `window` üzerine açılarak test ve hata ayıklama kolaylaştırıldı
  - Birim PDF, personel PDF, boş veri uyarısı ve çoklu personel gruplaması test edildi

- **📝 Güncellenen Dosyalar**:
  - Controllers/AccountController.cs
  - Views/PRS/Mesai/Index.cshtml
  - README.md
  - IMPLEMENTATION_SUMMARY_TR.md

---

## v3.4.0 - 9 Temmuz 2026 - ✅ PRODUCTION

### 💰 Mesai Ödeme (Overtime Payment) Modülü

- **✅ İki Aşamalı Ödeme Hesaplama Akışı**:
  - Aşama 1 (Yükle): Seçilen dönem ve birim için personel listesini yükle (ödeme=0)
  - Aşama 2 (Hesapla): Saatlik ücret oranlarını çek ve ödeme tutarını hesapla
  - Export butonları: Yükle → Hesapla işlemleri sonrasında etkinleşir

- **✅ Birim Bazlı Erişim Kontrolü**:
  - Admin kullanıcılar: Tüm birimler + "Tüm Personeller" seçeneği
  - Birim Amiri: Sadece kendi birimi (dropdown devre dışı)
  - API'de de yetkilendirme kontrolü mevcuttur

- **✅ Veri Sorgulaması**:
  - INNER JOIN ile mesai kaydı olan personelleri filtrele
  - SUM(toplam_saat) ile ay/yıl bazlı toplam saatleri hesapla
  - mesaisaat tablosundan en son saatlik_brut oranını çek
  - Ödeme hesaplama: toplam_saat × saatlik_brut

- **✅ Export İşlevleri**:
  - Excel (XLS) indir - HTML table → MIME type: application/vnd.ms-excel
  - PDF indir - html2pdf.js → A4 landscape, 8mm margin, scale=2
  - Yazdır - window.print() + CSS @media print rules

- **✅ Veritabanı Schema Güncellemesi**:
  - admin_kullanicilar tablosuna aktif_mi (BOOLEAN DEFAULT TRUE) sütunu eklendi

- **✅ UI/UX İyileştirmesi**:
  - Yıl/Ay seçimi (2024-2027, Ocak-Aralık)
  - GENEL TOPLAM satırı (toplamPersonel, toplamMesai, saatlikBrut, genelToplamOdeme)
  - Seçili Dönem ve Toplam Personel özet bilgileri
  - Toast mesajları (başarı, hata, uyarı) JavaScript utilities

- **✅ Üretime Dağıtım**:
  - FTP ile 38 dosya yüklendi (94.73.145.55)
  - Bootstrap CSS CDN'den yükleniyor
  - Menüye "💰 Mesai Ödeme" linki eklendi
  - Localhost:5010'da test edildi

- **📚 Yeni Dosyalar**:
  - Views/PRS/Mesai/MesaiOdemesi.cshtml
  - MESAI_ODEME_FEATURE_DOCUMENTATION.md

- **📝 Güncellenen Dosyalar**:
  - Controllers/PRS/MesaiController.cs
  - Views/Shared/_Layout.cshtml
  - Views/PRS/Mesai/Index.cshtml
  - Program.cs

---

## v3.3.0 - 7 Temmuz 2026 - ✅ PRODUCTION

### 🗄️ Admin Veritabanı Yedekleme/Geri Yükleme Güçlendirmesi

- **✅ Seçili Tablo Yedekleme**: Admin ekranında seçilen tabloların CREATE + INSERT script'i indiriliyor
- **✅ Güvenli Tablo Filtreleme**:
  - Regex ile tablo adı doğrulama (`^[A-Za-z0-9_]+$`)
  - `information_schema` üzerinden gerçek tablo listesi ile whitelist kontrolü
- **✅ Seçili Tablo Geri Yükleme**:
  - `.sql` uzantı ve maksimum 20 MB dosya doğrulaması
  - SQL statement parser ile yorum/quote ayrıştırması
  - Seçilmeyen tablolara ait komutların atlanması
- **✅ Opsiyonel Temizleme**: `clearSelectedTables=true` ile geri yükleme öncesi seçili tabloları `TRUNCATE`

### 📦 Ürün Toplu Yükleme Ön İzleme Akışı İyileştirmeleri

- **✅ Session Plan Token Akışı**:
  - Ön izleme sonucu session'da token ile saklanıyor
  - Uygulama adımında token doğrulaması yapılıyor
- **✅ Silinecek Ürün Ön İzlemesi**:
  - Excel'de olmayan ürünlerin silinme adayı listesi gösteriliyor
  - "Veritabanını temizle" seçeneği ile uygulama sırasında toplu silme yapılabiliyor
- **✅ Detaylı Güncelleme Görünürlüğü**:
  - Güncellenecek ürünler tablosunda eski → yeni alan karşılaştırması
  - Değişecek alan rozetleri (Ad/Birim/Grup/Barkod)
- **✅ Operasyon Logları**:
  - Preview/Apply işlemleri `stk_UrunTopluYuklemeLog` tablosunda tutuluyor
  - Son 10 işlem ekranda listeleniyor

### 🧩 Model ve View Güncellemeleri

- `UrunTopluYukleResult` modeline dosya adı, silme seçimi ve güncellenecek ürün listesi alanları eklendi
- `UrunTopluYukle.cshtml` ekranına:
  - Dosya adı gösterimi
  - Uyarı/hatalı satır tabloları
  - Güncellenecek ürün fark tablosu
  - Silinecek ürünler tablosu

### 📚 Güncellenen Dosyalar

- `Controllers/AdminController.cs`
- `Models/UrunTopluYukleResult.cs`
- `Views/STS/Admin/UrunTopluYukle.cshtml`

---

## v3.2.0 - 10 Haziran 2026 - ✅ PRODUCTION

### 🎨 Yeni Özellikler (Phase 2)

#### Sevkiyat Yönetimi Mimarisi Yenileme
- **✅ Sayfa Bölünmesi**: Tek sayfa → 3 ayrı sayfa
  - `/Sevkiyat/Index` - Navigation Hub (Card-based)
  - `/Sevkiyat/SevkBekleyenEksikler` - Bekleyen Eksikler (Durum='Bekliyor', SilindiMi=FALSE)
  - `/Sevkiyat/Gecmis` - Sevkiyat Geçmişi
  
- **✅ Filtre Sistemi Genişletilmesi**
  - Bağımsız Session keys: `LastSubeFilter`, `LastFirmaFilter`, `LastGrupFilter`
  - Bölüm, Firma, Grup filtreleri
  - Filtre badge'leri
  - Responsive dropdown'lar

#### Soft-Delete Özelliği
- **✅ Admin-Only Silme**: Modal ile sebep alınması
- **✅ Schema Güncellemeleri**:
  - `SilindiMi` (BOOLEAN) - Silme flag'i
  - `SilmeSebebi` (VARCHAR 500) - Silme nedeni
  - `SilmeTarihi` (DATETIME) - Silme tarihi
  - `SilenKullaniciId` (INT) - Silenin ID'si
  - `idx_silindi` (INDEX) - Performans

- **✅ Durum Enum Güncelleme**: 'Silindi' değeri eklendi

- **✅ HareketLog Entegrasyonu**: Tüm silme işlemleri loglaniyor

- **✅ Runtime Migration**: Kollar otomatik oluşturuluyor (ilk açılışta)

#### DepoSorumlusu Rol Genişletilmesi
- **✅ Tüm Şube Görme Yetkisi**: SubeOnay'da WHERE SubeId filtresi kaldırıldı
- **✅ Onay/Red Yetkisi**: OnayEt/Reddet'te rol kontrolü güncellendi
- **✅ ViewBag Implementasyonu**: `IsDepoSorumlusu` flag'i

#### Admin Email Konfigürasyonu
- **✅ Model Güncellemesi**: `KullaniciModel.Email` property'si
- **✅ View Kartı**: Profil sayfasında email ayarları bölümü
- **✅ EmailGuncelle Action**: POST işlemi ve validasyon
- **✅ Runtime Migration**: `admin_kullanicilar.email` column otomatik oluşturması

#### UI İyileştirmeleri
- **✅ STS Home Page**: Büfe Sevkiyat Günleri bildirimi eklendi (ℹ️ Badge)
- **✅ Modal Tasarımı**: Silme işlemi modal'ı
- **✅ Responsive Tables**: `data-label` attributes
- **✅ Badge Styling**: Durum gösterimi

### 🔧 Teknik Iyileştirmeler

#### Controller Güncellemeleri
- **SevkiyatController.cs** (1100+ satır)
  - `Index()` - Navigation hub (YENİ)
  - `SevkBekleyenEksikler()` - Bekleyen eksikler (YENİ)
  - `Gecmis()` - Sevkiyat geçmişi (YENİ)
  - `SilEksik()` - Soft-delete (YENİ)
  - `EnsureEksikKaydiSilColumnAsync()` - Runtime migration (YENİ)

- **KullaniciController.cs**
  - `Profil()` - Email okuma (UPDATED)
  - `EmailGuncelle()` - Email güncelleme (YENİ)
  - `EnsureAdminEmailColumnAsync()` - Runtime migration (YENİ)

#### View Güncellemeleri
- `Views/STS/Sevkiyat/Index.cshtml` (YENİ)
- `Views/STS/Sevkiyat/SevkBekleyenEksikler.cshtml` (YENİ)
- `Views/STS/Sevkiyat/Gecmis.cshtml` (YENİ)
- `Views/STS/Sevkiyat/SubeOnay.cshtml` (UPDATED - ViewBag pattern)
- `Views/PRS/Kullanici/Profil.cshtml` (UPDATED - Email kartı)

#### Model Güncellemeleri
- `Models/PRS/KullaniciModel.cs` - Email property eklendi

#### Migration Files
- `sql/migration_add_silindi_status.sql` (YENİ)
- `sql/migration_add_admin_email.sql` (YENİ)

### 🧪 Test & QA

- ✅ Build: 0 Errors, 44 Warnings
- ✅ Local Testing: Tüm senaryolar pass
- ✅ Database Connectivity: OK
- ✅ Session Management: Verified
- ✅ Role-based Access: Tested
- ✅ Modal Functionality: Working
- ✅ Form Validation: Active

### 🚀 Deployment

- ✅ Release Build: 167.3 saniye
- ✅ FTP Upload: 10.6 saniye, 32 dosya
- ✅ Manifest Update: 4 başarılı dosya
- ✅ Runtime Migration: Otomatik

### 📚 Dokümantasyon

- ✅ IMPLEMENTATION_SUMMARY_TR.md - Fase 2 detayları
- ✅ TECHNICAL_DOCUMENTATION.md - 10 Haziran bölümü
- ✅ DEPLOYMENT_GUIDE.md - Migration bilgisi
- ✅ FASE_2_SEVKIYAT_YONETIMI_TR.md - Comprehensive guide (YENİ)
- ✅ README.md - Başlık güncelleme

---

## v3.1.0 - 25 Mayıs 2026 - ✅ PRODUCTION

### 🎨 CSS Tasarım Sistemi Tamamlandı

#### Dosya Yapısı
- `wwwroot/css/variables.css` - 50+ CSS variables
- `wwwroot/css/utilities.css` - 100+ utility classes
- `wwwroot/css/components.css` - 50+ component styles
- `wwwroot/css/layout.css` - Layout sistemleri
- `wwwroot/css/forms.css` - Form styling
- `wwwroot/css/dashboard.css` - Dashboard componenti
- `wwwroot/css/site.css` - Global styles

#### Modernize Edilen Views (9 dosya)
1. DetailedRapor.cshtml
2. Sikayet/Index.cshtml
3. ResmiTatil/Index.cshtml
4. Kullanici/Index.cshtml
5. Kullanici/Profil.cshtml
6. Kullanici/Liste2.cshtml
7. Account/Register.cshtml
8. Account/StsLogin.cshtml
9. Account/Login.cshtml

#### Stil Güncellemeleri
- ✅ Gradient başlıklar
- ✅ Card-based layout
- ✅ Emoji integrasyonu
- ✅ Bootstrap 5 entegrasyonu
- ✅ Responsive design
- ✅ Badge styling
- ✅ Alert color coding

---

## v3.0.0 - 14-25 Mayıs 2026 - ✅ PRODUCTION

### 🎨 Dashboard & Raporlar

#### STS Dashboard UI
- Hızlı Erişim butonları merkezlendi
- Bilgilendirme kartları daraltıldı
- Profesyonel tasarım

#### Haftalık Raporlar → Tüm Zamanlar
- `Controllers/RaporStsController.cs` - WHERE HaftaNo filtresi kaldırıldı
- Tüm veriler gösterilmeye başlandı
- En Çok Eksik Ürünler (10)
- Geç Giren Şubeler
- Acil Siparişler

### 🏷️ Sevkiyat Grup Filtrelemesi

#### Yeni Filtre
- Üçüncü dropdown: "Grup Seçin"
- Dinamik grup listesi (SQL'den)
- Tablo başlığına Grup sütunu
- Badge'lere "Grup: [seçili]" eklendi
- Session'da `LastGrupFilter` kaydı

#### SQL Güncellemeler
```csharp
SELECT ... FROM stk_Urun 
WHERE Grup IS NOT NULL AND Grup != ''
ORDER BY Grup

// WHERE clause'a eklendi:
AND u.Grup = @Grup
```

### 🐛 Bug Fixes & Stabilization

#### Tarih Formatı Düzeltmesi
- DateTime inline toString() çalışmıyordu
- Pre-format pattern uygulandı

#### Dinamik Object Binding
- RuntimeBinderException null properties'de
- Early extraction pattern kullanıldı

#### Build & Deployment
- Application lock hatası çözüldü
- Build başarılı: 0 errors, 45 warnings

---

## v2.2.0 - 5-6 Haziran 2026 - ✅ PRODUCTION

### 🗄️ Admin Veritabanı Yönetimi

- Tablo seçmeli SQL yedekleme
- Geri yükleme akışı (.sql dosyalarından)
- Admin paneline ekle: "Veritabanı Yedekleme" bağlantısı

### 📊 Ürün Toplu Yükleme Yeniden Tasarlandı

#### İki Aşamalı Akış
1. **Ön İzleme**: Eklenecek, güncellenecek, uyarı, silinecek kayıtlar gösterilir
2. **Uygula**: Session tokenle CSRF koruması, işlem log'u

#### İyileştirmeler
- Hata ve uyarılar ayrı bloklar
- Özet kartları
- Silme önizlemesi
- Güvenli barkod çakışma yönetimi

### 📈 Sevkiyat Excel Dışa Aktarımı

- Filtrelenmiş veriler Excel'e aktarılır
- EPPlus 7.0.7 kütüphanesi

---

## v2.1.0 - 15-20 Mayıs 2026 - ✅ PRODUCTION

### 🔧 Yükseltme & Stabilizasyon

- DateTime format düzeltmeleri
- RuntimeBinderException çözümleri
- Build prosedürü iyileştirildi

---

## v2.0.0 - 14 Mayıs 2026 - ✅ PRODUCTION

### 🆕 Sevkiyat Filtre Özelliği

#### Filter Hafızası
- Session-based Bölüm ve Firma filter'leri
- Sayfa yenilenişinde restore
- Diğer işlemler sonrası korunuyor

#### UI İyileştirmeleri
- "Güncelle" butonu (otomatik submit değil)
- Badge gösterimi (seçili filtreler)
- Alert kutusu (aktif filtreler)
- Dropdown vurgulama

#### Excel Export
- Filtrelenmiş veriler
- EPPlus 7.0.7

---

## v1.0.0 - İlk Release - 2026 Başı

### 🏢 Temel Yapı

- ASP.NET Core 9.0 MVC
- MySQL veritabanı
- PRS (Personel Yönetimi) modülü
- STS (Sevkiyat Takip) modülü
- Session-based authentication
- Bootstrap 5 UI

---

## 📊 Version Özeti

| Versiyon | Tarih | Durum | Büyük Özellik |
|----------|-------|-------|----------------|
| 3.3.0 | 7 Temmuz | ✅ PROD | Admin SQL geri yükleme güvenliği, ürün ön izleme akışı |
| 3.2.0 | 10 Haziran | ✅ PROD | Sevkiyat Fase 2, Soft-Delete, Email |
| 3.1.0 | 25 Mayıs | ✅ PROD | CSS System, UI Modernizasyon |
| 3.0.0 | 14-25 Mayıs | ✅ PROD | Dashboard, Raporlar, Grup Filtrelemesi |
| 2.2.0 | 5-6 Haziran | ✅ PROD | Admin Tools, Excel Upload |
| 2.1.0 | 15-20 Mayıs | ✅ PROD | Bug Fixes & Stabilization |
| 2.0.0 | 14 Mayıs | ✅ PROD | Sevkiyat Filtreleri |
| 1.0.0 | 2026 Başı | ✅ PROD | Initial Release |

---

## 🚀 Build & Deploy Stats

### v3.2.0 Deployment
- Build Time: 167.3s (Release)
- Publish: 20.76 MB
- Upload: 32 files (10.6s)
- Warnings: 44
- Errors: 0 ✅

### Database Changes
- New Columns: 4 (stk_EksikKaydi)
- New Column: 1 (admin_kullanicilar)
- New Indexes: 2
- New Constraints: 1 (FK - optional)

### Code Changes
- Files Modified: 8
- Files Created: 4 (views + migration files)
- Lines Added: ~500
- New Methods: 5

---

## 📞 Breaking Changes

❌ **Yok**: Backward compatibility tamamen korunmuş

- Eski `/Sevkiyat/Index` URI'ları yönlendirilmek değil, yeni `/SevkBekleyenEksikler`'e yönlendir
- Soft-delete için `WHERE SilindiMi = FALSE` filtresini tüm query'lere ekle

---

## 🔄 Deprecation Policy

- Eski `Index.cshtml` hâlâ çalışacak (navigation olarak)
- Session key'ler: `LastSubeFilter` → `LastGrupFilter` (yeni adlar tercih ediliyor)

---

## 📝 Bilinecek Noktalar

### Performance
- N+1 query sorunu hâlâ var (future optimization)
- Pagination yok (10K+ rows)
- Caching yok

### Security
- XSS riski (@Html.Raw() 7 yerinde)
- Şifre hashing yok (plain text)
- Logging audit trail eksik (HareketLog haricinde)

### Testing
- Unit test yok
- Integration test yok
- E2E test yok (manual only)

---

**Generated**: 7 Temmuz 2026  
**Tool**: Automated Changelog Generator  
**Format**: Markdown + Semantic Versioning
