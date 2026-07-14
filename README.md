# 🏢 GAMABEL MVC

Gamabel MVC, iki ana alan içeren ASP.NET Core 9.0 uygulamasıdır:
- **PRS** (Personel / Mesai / Puantaj Yönetimi)
- **STS** (Sevkiyat Takip Sistemi)

## 🆕 10 Temmuz 2026 Güncellemesi - Ek Mesai PDF Hotfix

✅ **Ek Mesai PDF dışa aktarımı uçtan uca çalışır hale getirildi**:
- ✨ `PDF (Birim)` ve `PDF (Personel)` butonları aktif akışla çalışıyor
- ✨ Birim bazlı kurumsal PDF yerleşimi eklendi
- ✨ Personel bazlı gruplu PDF yerleşimi ve alt toplamlar eklendi
- ✨ `window.print()` tabanlı güvenli yazdırma akışı ile popup engelleri kaldırıldı
- ✨ Boş veri durumunda kullanıcı uyarısı gösteriliyor

✅ **PRS hesap açılış akışı düzeltildi**:
- ✨ Yeni kayıt olan kullanıcılar için `aktif_mi = 1` atanıyor
- ✨ Test kullanıcıları kayıt sonrası doğrudan giriş yapabiliyor

**Doğrulanan Sonuçlar**:
- ✅ PDF (Birim): başlık, tablo, genel toplam, imza alanları, zaman damgası
- ✅ PDF (Personel): personel gruplama, alt toplamlar, alfabetik sıralama
- ✅ Boş veri: `Yazdırılacak veri yok` toast mesajı
- ✅ Çoklu personel: her personel için ayrı tablo üretimi

**Güncellenen Dosyalar**:
- Views/PRS/Mesai/Index.cshtml
- Controllers/AccountController.cs
- CHANGELOG_TR.md
- IMPLEMENTATION_SUMMARY_TR.md

## 🆕 9 Temmuz 2026 Güncellemesi - Mesai Ödeme Modülü

✅ **Mesai Ödeme (Overtime Payment) Özelliği Eklendi**:
- ✨ İki aşamalı ödeme hesaplama: Yükle → Hesapla
- ✨ Birim bazlı erişim kontrolü (Admin: tüm birimler, Birim Amiri: kendi birimi)
- ✨ Saatlik brüt ücret × toplam mesai saati = ödeme tutarı
- ✨ Export fonksiyonları: Excel, PDF, Yazdır
- ✨ GENEL TOPLAM satırı ile özet veriler

**Yeni Özellikler**:
- Yıl (2024-2027) ve Ay (Ocak-Aralık) seçimi
- Personel listesi yükleme (hesapla=false)
- Ödeme hesaplama (hesapla=true)
- Toast mesajları ve loading state management
- Responsive tablo tasarımı (Bootstrap 5.3.0 CDN)

**Yeni Sayfa**: `/Mesai/MesaiOdemesi`
- Menü: ⏰ Ek Mesai → 💰 Mesai Ödeme
- API Endpoint: GET `/Mesai/GetMesaiOdemeleri`

**Dosya Değişiklikleri**:
- Controllers/PRS/MesaiController.cs (2 yeni method: MesaiOdemesi, GetMesaiOdemeleri)
- Views/Shared/_Layout.cshtml (Bootstrap/jQuery CDN, Menu item)
- Views/PRS/Mesai/Index.cshtml (Ödeme hesaplama kodu kaldırıldı)
- Program.cs (Development mode test user seed)
- Database: admin_kullanicilar tablosuna aktif_mi sütunu eklendi

**Test Sonuçları**:
- ✅ Test kullanıcı (test/test): Başarılı giriş ve birim seçimi
- ✅ AÇIK PAZAR birimi: 2 personel, 15.22 toplam saat
- ✅ Ödeme hesaplama: 349.71₺ × 15.22 saat = 5,322.59₺ toplam
- ✅ Excel/PDF export: Çalışıyor
- ✅ Üretimde aktif: 38 dosya FTP ile dağıtıldı

**Kapsamlı Dokümantasyon**: [MESAI_ODEME_FEATURE_DOCUMENTATION.md](MESAI_ODEME_FEATURE_DOCUMENTATION.md)

## 🆕 7 Temmuz 2026 Güncellemesi - Admin Veri Operasyonları

✅ **Admin SQL Yedekleme / Geri Yükleme Akışı Güçlendirildi**:
- ✨ Seçili tablolar için SQL yedek alma (CREATE + INSERT)
- ✨ Seçili tabloları `.sql` dosyasından geri yükleme
- ✨ Dosya doğrulama: yalnızca `.sql`, maksimum 20 MB
- ✨ Güvenli tablo doğrulama: regex + mevcut tablo listesi
- ✨ Transaction tabanlı geri yükleme (rollback/commit)

✅ **Ürün Toplu Yükleme Akışı Genişletildi**:
- ✨ Ön izleme planı session token ile saklanıyor
- ✨ Ön izlemeyi onayla ve uygula adımı eklendi
- ✨ "Excel'de olmayan ürünleri sil" opsiyonu
- ✨ Güncellenecek ürünler için eski → yeni alan karşılaştırma tablosu
- ✨ Silinecek ürünler listesi (uygulama öncesi görünür)

**Dosya Değişiklikleri**:
- Controllers/AdminController.cs
- Models/UrunTopluYukleResult.cs
- Views/STS/Admin/UrunTopluYukle.cshtml

## 🆕 11 Haziran 2026 Güncellemesi - Fase 2 Tamamlanması

✅ **DepoSorumlusu Rol Onaylama İşleyişi Tamamlandı**:
- ✨ SubeOnay sayfasına tam erişim sağlandı
- ✨ Rol özel UI (Başlık, uyarı, iki tablo yapısı)
- ✨ Depo siparişleri görüntüleme (OnayBekliyor durumu)
- ✨ Menüye "✓ Şube Onay" linki eklendi
- ✨ Rol bazında filtreleme (sadece kendi depo/şubesinin verileri)

**Yeni Özellikler**:
- DepoSorumlusu: Şubelerden gelen ürünler + depo siparişlerini görebiliyor
- SubePersoneli: Sadece şubelerden gelen ürünleri görebiliyor
- Her iki rol: Sadece kendi depo/şubesinin verileri görülebiliyor
- Onayla/Reddet butonları her iki tabloda da çalışıyor

**Dosya Değişiklikleri**:
- Controllers/STS/SevkiyatController.cs (SubeOnay action)
- Views/STS/Sevkiyat/SubeOnay.cshtml (Sayfa tasarımı)
- Views/Shared/_Layout.cshtml (Menü güncelleme)

## 🆕 10 Haziran 2026 Güncellemesi - Fase 2

✅ **Sevkiyat Yönetimi Sayfa Mimarisi Bölünmesi**:
- Tek sayfa → 3 ayrı sayfa (Index/Hub, SevkBekleyenEksikler, Gecmis)
- Ayrı filtre session key'leri
- Card-based navigation hub
- Responsive tablo tasarımı

✅ **Soft-Delete Özelliği**:
- Admin-only silme işlemi (modal ile sebebi alma)
- SilindiMi, SilmeSebebi, SilmeTarihi, SilenKullaniciId sütunları
- HareketLog kaydı (audit trail)
- Runtime schema migration

✅ **DepoSorumlusu Rol Genişletilmesi**:
- Tüm şubeleri görme yetkisi
- Tüm şubelerin sevkiyat onayı/reddi

✅ **Admin Email Konfigürasyonu**:
- Profil sayfasında email ayarları kartı
- Gelecekte report gönderme için hazır
- Runtime email column migration

## 🆕 25 Mayıs 2026 Güncellemesi

✅ **CSS Tasarım Sistemi Tamamlandı**:
- 7-dosya CSS mimarisi (50+ değişken, 100+ komponent, 700+ utility)
- 9 view dosyası modernize edildi (inline styles kaldırıldı)
- Card-based layout tüm listelere uygulandı
- Gradient başlıklar ve emoji'li arayüzler eklendi

✅ **Modernize Edilen Views**:
1. 📊 DetailedRapor.cshtml - Detaylı rapor filtre ve tablosu
2. 📋 Sikayet/Index.cshtml - Şikayet yönetim listesi
3. 📅 ResmiTatil/Index.cshtml - Tatil takvimi
4. 👥 Kullanici/Index.cshtml - Kullanıcı listesi
5. 👤 Kullanici/Profil.cshtml - Profil ve şifre kartları
6. 👥 Kullanici/Liste2.cshtml - Personel listesi + Excel import
7. 📝 Account/Register.cshtml - Kayıt formu
8. 🔑 Account/StsLogin.cshtml - STS giriş
9. 🚪 Account/Login.cshtml - Ana giriş

✅ **14-25 Mayıs Güncellemeleri**:
1. 🎨 Dashboard UI iyileştirildi (Hızlı Erişim, Bilgilendirme Kartları)
2. 📊 Haftalık Raporlar → Tüm Zamanlar Raporu
3. 🏷️ Sevkiyat Yönetimi - Grup Filtrelemesi eklendi
4. 📈 30+ Sorun analiz ve çözüm önerileri (PROJECT_ANALYSIS_TR.md)

## 📚 Güncel Teknoloji

- **.NET**: 9.0
- **Veritabanı**: MySQL (MySqlConnector)
- **UI**: Razor Views + Bootstrap 5
- **Excel**: EPPlus 7.0.7
- **Authentication**: Session-based

## 📁 Güncel Klasör Yapısı

```
gamabelmvc/
├── Controllers/
│   ├── AccountController.cs, AdminController.cs, HomeController.cs
│   ├── PRS/                    # Personel modülü
│   │   ├── HizliMesaiGirisiController.cs
│   │   ├── KullaniciController.cs
│   │   ├── MesaiController.cs
│   │   ├── PuantajController.cs
│   │   ├── ResmiTatilController.cs
│   │   └── SikayetController.cs
│   └── STS/                    # Sevkiyat modülü
│       ├── EksikController.cs
│       ├── RaporController.cs
│       ├── RaporStsController.cs
│       ├── SevkiyatController.cs
│       └── SiparisController.cs
├── Models/
│   ├── PRS/                    # Personel modelleri
│   └── STS/                    # Sevkiyat modelleri
├── Views/
│   ├── PRS/                    # Personel görünümleri
│   ├── STS/                    # Sevkiyat görünümleri
│   └── Shared/
├── Services/
│   └── DbConnectionFactory.cs
├── wwwroot/
├── sql/
└── 📄 Dokümantasyon Dosyaları:
    ├── README.md (bu dosya)
    ├── TECHNICAL_DOCUMENTATION.md ✨ YENİ
    ├── IMPLEMENTATION_SUMMARY_TR.md
    ├── FILTER_FEATURE_DOCUMENTATION.md
    ├── PROJECT_ANALYSIS_TR.md
    ├── HIZLI_MESAI_GIRISI_KILAVUZ.md
    └── DEPLOYMENT_GUIDE.md
```

## ⚙️ Canlı Çalıştırma

### Hızlı Başlangıç

```powershell
# Uygulamayı durdur ve temizle
.\dev-tools.ps1 -Action stop

# Rebuild et
.\dev-tools.ps1 -Action rebuild

# Çalıştır (localhost:5010)
.\dev-tools.ps1 -Action run -Port 5010
```

### Manual Komutlar

```powershell
# Build
dotnet build

# Run
dotnet run

# Test URL'leri
http://localhost:5010/              # Ana sayfa
http://localhost:5010/Account/StsLogin  # STS giriş
http://localhost:5010/STS/Home/Index    # STS Dashboard
```

## 🔒 Kimlik Doğrulama

| Sistem | Kullanıcı | Şifre | Rol |
|--------|-----------|-------|-----|
| **PRS** | personel1 | 123456 | Personel |
| **STS** | depo1 | 123456 | DepoSorumlusu |
| **Admin** | admin | admin | Admin |

## 📊 Son Güncellemeler (14-25 Mayıs 2026)

### Dashboard UI (14 Mayıs)
- ✅ Hızlı Erişim butonları merkezlendirildi
- ✅ Bilgilendirme kartları daraltıldı (kompakt tasarım)
- **Dosya**: `Views/STS/Home/Index.cshtml`

### Haftalık Raporlar (14 Mayıs)
- ✅ Hafta filtresi kaldırıldı → Tüm zamanlar gösteriliyor
- ✅ Başlık güncellendi: "Haftalık Raporlar" → "Tüm Şubelerin Eksikleri"
- **Dosya**: `Controllers/RaporStsController.cs`, `Views/STS/RaporSts/Index.cshtml`

### Sevkiyat Yönetimi (14-15 Mayıs)
- ✅ 3. Filtre eklendi: "Grup Seçin" dropdown
- ✅ Tablo başlığına "Grup" sütunu eklendi
- ✅ Badge'lere "Grup: [seçili]" gösteriliyor
- ✅ Session-based filtre hafızası
- **Dosya**: `Controllers/SevkiyatController.cs`, `Views/STS/Sevkiyat/Index.cshtml`

### Proje Analizi (20-25 Mayıs)
- ✅ 30+ Spesifik sorun tanımlandı
- ✅ Security Score: 4/10 🔴
- ✅ İyileştirme önerileri: 20+ madde
- **Dosya**: `PROJECT_ANALYSIS_TR.md`

## 📚 Dokümantasyon

| Dosya | İçerik | Güncelleme |
|-------|--------|----------|
| **README.md** | Hızlı başlangıç (bu dosya) | 7 Temmuz |
| **CSS_DESIGN_SYSTEM.md** | ✨ **YENİ** CSS mimarisi, komponentler, utilities (500+ satır) | 25 Mayıs |
| **TECHNICAL_DOCUMENTATION.md** | Kapsamlı teknik detaylar + 7 Temmuz admin güncellemeleri | 7 Temmuz |
| **IMPLEMENTATION_SUMMARY_TR.md** | Uygulama değişiklikleri + CSS modernizasyon (1000+ satır) | 25 Mayıs |
| **FILTER_FEATURE_DOCUMENTATION.md** | Filtre özellikleri + CSS güncellemeleri (1000+ satır) | 25 Mayıs |
| **PROJECT_ANALYSIS_TR.md** | Sorun ve çözüm önerileri (200+ satır) | 25 Mayıs |
| **HIZLI_MESAI_GIRISI_KILAVUZ.md** | Hızlı mesai modülü rehberi | 15 Mayıs |
| **DEPLOYMENT_GUIDE.md** | Deploy prosedürü | 🆕 Planlı |

## 🚀 Kısa Yol Komutları

```powershell
# Hızlı build & run
.\dev-tools.ps1 -Action rebuild; .\dev-tools.ps1 -Action run

# İşlem kilidi hatası çözümü
Get-Process gamabelmvc -ErrorAction SilentlyContinue | Stop-Process -Force

# Database backup
.\source-backup.ps1
```

## ⚠️ Bilinen Sorunlar

| Sorun | Çözüm | Durum |
|-------|-------|-------|
| Application lock hatası | `Stop-Process -Force` | ✅ Çözüldü |
| DateTime formatting | Literal text parsing | ✅ Çözüldü |
| Dropdown seçimi kaybolması | ViewBag persistence | ✅ Çözüldü |
| Hafta filtresi tutarsızlığı | Her ikisi tüm zamanlar | ✅ Çözüldü |

## 🔔 İlk 5 Yapılması Gereken İşlem

1. **CSRF Protection** (30 min) 🚨
   - [ValidateAntiForgeryToken] attribute ekle
   - @Html.AntiForgeryToken() form'lara ekle

2. **Şifre Hashing** (1 saat) 🚨
   - BCrypt.Net.BCrypt.HashPassword() kullan
   - Plain text şifreler güvenlik tehdidi

3. **Logging Sistemi** (2 saat) 📋
   - Serilog veya built-in logging ekle
   - Hatalar izlenemez durumda

4. **Pagination** (3 saat) 📄
   - LIMIT/OFFSET ekle büyük tablolara
   - 10K+ rows belleğe yükleniyor

5. **ViewModel Pattern** (4 saat) 🎯
   - ViewBag yerine strongly-typed models
   - Type safety sağla

**Detaylı tavsiyeler**: `PROJECT_ANALYSIS_TR.md`

## 📞 Sorun Giderme

### Build Başarısız
```powershell
# Process kilitliyse
.\dev-tools.ps1 -Action stop
dotnet clean
dotnet build
```

### Database Bağlantı Hatası
```
appsettings.json'da Server ayarlarını kontrol et
MySQL çalışıyor mu? -> systemctl status mysql
```

### View Hataları
```
Razor syntax hataları için -> Build çıktısını kontrol et
Dynamic object binding hatası -> Null check ekle
```

## 📋 Geliştirme Kuralları

- ✅ Parametrized SQL queries (zorunlu)
- ✅ UTF-8 encoding (varsayılan)
- ✅ Türkçe karakter desteği
- ✅ Bootstrap 5 grid sistemi
- ❌ Hardcoded connections (yasak)
- ❌ Plain text passwords (yasak)
- ❌ Direct database calls (ViewModels kullan)

## 📊 Proje İstatistikleri

| Metrik | Değer |
|--------|-------|
| Controllers | 11 |
| Models | 13 |
| Views | 50+ |
| Database Tables | 15+ |
| Lines of Code | 10,000+ |
| Build Time | ~3 saniye |
| Test Coverage | ⚠️ Eksik |

## ✅ Build ve Deployment

```
✅ Build Status: Success (0 errors, 45 warnings)
✅ Database: MySQL Connected
✅ Authentication: Session-based
✅ Security: Parametrized queries
✅ Performance: Acceptable
🟡 Logging: Missing (KRITIK)
🟡 Testing: Missing (ÖNEMLI)
```

## 📞 İletişim

**Sorular**: Geliştirici ekibi  
**Hatalar**: `PROJECT_ANALYSIS_TR.md`  
**Deployment**: `DEPLOYMENT_GUIDE.md`

---

**Son Güncelleme**: 7 Temmuz 2026  
**Versiyon**: 3.3  
**Durum**: ✅ Üretim'de


#   g a m a b e l  
 #   g a m a b e l v 2  
 