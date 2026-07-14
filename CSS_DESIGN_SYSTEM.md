# 🎨 CSS TASARIM SİSTEMİ DOKÜMANTASYONU

**Tarih**: 25 Mayıs 2026  
**Versiyon**: 1.0  
**Durum**: Tamamlandı (9 view tamamlandı, 4 karmaşık view kalan)

## 📋 İçindekiler

1. [Sistem Mimarisi](#sistem-mimarisi)
2. [CSS Dosyaları](#css-dosyaları)
3. [Tasarım Değişkenleri](#tasarım-değişkenleri)
4. [Komponent Kütüphanesi](#komponent-kütüphanesi)
5. [Utility Sınıfları](#utility-sınıfları)
6. [Responsive Tasarım](#responsive-tasarım)
7. [Modernize Edilen Views](#modernize-edilen-views)
8. [Kalan Çalışmalar](#kalan-çalışmalar)

---

## 🏗️ Sistem Mimarisi

### Dosya Yapısı

```
wwwroot/css/
├── site.css              (Ana import dosyası)
├── variables.css         (50+ CSS değişkeni)
├── components.css        (100+ komponent sınıfı)
├── layout.css            (Sayfa yapısı + navbar)
├── forms.css             (Form elemanları)
├── tables.css            (Tablo stilleri)
├── utilities.css         (700+ utility sınıfı)
└── dashboard.css         (Dashboard-spesifik komponentler)
```

### Import Sırası (site.css)

```css
@import "variables.css";      /* Değişkenler önce */
@import "components.css";     /* Ardından komponentler */
@import "layout.css";         /* Sayfa yapısı */
@import "forms.css";          /* Form stilleri */
@import "tables.css";         /* Tablo stilleri */
@import "dashboard.css";      /* Dashboard özel */
@import "utilities.css";      /* Utility'ler sonra */
```

**Neden bu sıra?**
- Değişkenler tüm dosyalarda kullanılmalı
- Komponentler değişkenleri referans alır
- Utility'ler en spesifik olduğundan en sona gelir

---

## 📄 CSS Dosyaları

### 1. variables.css (80+ satır)

**Amaç**: Tüm tasarım tokenleri (renkler, boşluklar, tipografi) bir yerde tanımla

**Renkler**:
```css
--gbl-primary: #283593              /* Koyu mavi */
--gbl-secondary: #1565c0            /* Açık mavi */
--gbl-success-light: #c8e6c9        /* Açık yeşil */
--gbl-danger-light: #ffebee         /* Açık kırmızı */
--gbl-warning: #fff3e0              /* Açık turuncu */
--gbl-info-light: #b3e5fc           /* Açık cyan */
--gbl-dark: #1a1a1a                 /* Siyah */
--gbl-gray: #9e9e9e                 /* Gri */
--gbl-white: #ffffff                /* Beyaz */
--gbl-light-gray: #f5f5f5           /* Açık gri */
--gbl-lighter-gray: #fafafa         /* Çok açık gri */
```

**Boşluk** (8 aşamalı ölçek):
```css
--gbl-spacing-xs: 4px;
--gbl-spacing-sm: 8px;
--gbl-spacing-md: 16px;
--gbl-spacing-lg: 24px;
--gbl-spacing-xl: 32px;
--gbl-spacing-2xl: 48px;
--gbl-spacing-3xl: 64px;
```

**Tipografi**:
```css
--gbl-font-family: 'Segoe UI', Tahoma, Geneva, sans-serif;
--gbl-font-size-xs: 11px;
--gbl-font-size-sm: 12px;
--gbl-font-size-md: 14px;
--gbl-font-size-lg: 16px;
--gbl-font-size-xl: 18px;
--gbl-font-size-2xl: 22px;
--gbl-font-size-3xl: 28px;
```

**Gölgeler** (Derinlik ölçeği):
```css
--gbl-shadow-xs: 0 1px 2px rgba(0,0,0,0.05);
--gbl-shadow-sm: 0 1px 3px rgba(0,0,0,0.1);
--gbl-shadow-md: 0 4px 6px rgba(0,0,0,0.1);
--gbl-shadow-lg: 0 10px 25px rgba(0,0,0,0.15);
--gbl-shadow-xl: 0 20px 50px rgba(0,0,0,0.2);
```

### 2. components.css (300+ satır)

**Amaç**: Tekrar kullanılabilir komponent sınıfları

**Buton Komponentleri**:
```css
.btn { /* Bootstrap temel */ }
.btn-primary { background: var(--gbl-primary); }
.btn-success { background: #28a745; }
.btn-danger { background: #dc3545; }
```

**Kart Komponentleri**:
```css
.card {
  background: white;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  box-shadow: var(--gbl-shadow-sm);
}

.card-header {
  padding: 16px;
  background: #f5f5f5;
  border-bottom: 1px solid #e0e0e0;
  font-weight: 600;
}
```

**Badge Komponentleri**:
```css
.badge { /* Bootstrap default */ }
.badge-primary { background: var(--gbl-primary); }
.badge-success { background: #28a745; }
.badge-warning { background: #ffc107; color: #000; }
.badge-danger { background: #dc3545; }
```

### 3. layout.css (470+ satır)

**Amaç**: Sayfa yapısı, navbar, footer, scrollbar vb.

**Navbar Komponentleri**:
```css
.gbl-navbar {
  background: linear-gradient(135deg, #1a237e 0%, #283593 50%, #1565c0 100%);
  box-shadow: 0 2px 12px rgba(0,0,0,.15);
}

.gbl-navbar .navbar-brand {
  color: #fff;
  font-weight: 800;
  font-size: 18px;
  letter-spacing: 0.5px;
}

.gbl-navbar .nav-link {
  color: rgba(255,255,255,0.85);
  font-weight: 500;
  padding: 10px 16px;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.gbl-navbar .nav-link:hover {
  color: #fff;
  background: rgba(255,255,255,0.15);
}

.gbl-navbar .nav-link.active-page {
  color: #fff;
  background: rgba(255,255,255,0.2);
  font-weight: 600;
}
```

**Scrollbar Özelleştirmesi**:
```css
::-webkit-scrollbar {
  width: 8px;
}

::-webkit-scrollbar-track {
  background: #f1f1f1;
}

::-webkit-scrollbar-thumb {
  background: #888;
  border-radius: 4px;
}

::-webkit-scrollbar-thumb:hover {
  background: #555;
}
```

### 4. forms.css (250+ satır)

**Amaç**: Form kontrolleri, validasyon, input tarzları

**Form Grupları**:
```css
.form-group {
  margin-bottom: 16px;
}

.form-label {
  font-weight: 600;
  color: #333;
  margin-bottom: 6px;
  display: block;
}

.form-control {
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  transition: all 0.2s;
}

.form-control:focus {
  border-color: var(--gbl-primary);
  box-shadow: 0 0 0 3px rgba(40,53,147,0.1);
}
```

**Validasyon Stilleri**:
```css
.is-invalid { border-color: #dc3545; }
.is-valid { border-color: #28a745; }
.invalid-feedback { color: #dc3545; font-size: 12px; }
.valid-feedback { color: #28a745; font-size: 12px; }
```

### 5. tables.css (200+ satır)

**Amaç**: Tablo varyantları, responsive tasarım

**Tablo Stilleri**:
```css
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.table th {
  background: #f5f5f5;
  padding: 12px;
  text-align: left;
  font-weight: 600;
  border-bottom: 2px solid #ddd;
}

.table td {
  padding: 12px;
  border-bottom: 1px solid #e0e0e0;
}

.table-hover tbody tr:hover {
  background: #f9f9f9;
}

.table-striped tbody tr:nth-child(odd) {
  background: #fafafa;
}
```

**Mobile Responsive**:
```css
@media (max-width: 768px) {
  .table-mobile-stack {
    display: block;
    width: 100%;
  }

  .table-mobile-stack thead {
    display: none;
  }

  .table-mobile-stack tbody tr {
    display: block;
    margin-bottom: 15px;
    border: 1px solid #ddd;
  }

  .table-mobile-stack td {
    display: block;
    text-align: right;
    padding-left: 50%;
    position: relative;
  }

  .table-mobile-stack td:before {
    content: attr(data-label);
    position: absolute;
    left: 6px;
    font-weight: 600;
    text-align: left;
  }
}
```

### 6. utilities.css (600+ satır)

**Amaç**: Tekil amaçlı utility sınıfları

**Metin Utilities**:
```css
.text-center { text-align: center; }
.text-left { text-align: left; }
.text-right { text-align: right; }
.text-muted { color: #9e9e9e; }
.text-dark { color: #1a1a1a; }
.font-bold { font-weight: 700; }
.font-semibold { font-weight: 600; }
```

**Boşluk Utilities**:
```css
.p-1 { padding: 4px; }
.p-2 { padding: 8px; }
.p-3 { padding: 16px; }
.p-4 { padding: 24px; }
.m-1 { margin: 4px; }
.m-2 { margin: 8px; }
.m-3 { margin: 16px; }
.mt-1 { margin-top: 4px; }
.mb-1 { margin-bottom: 4px; }
```

**Flexbox Utilities**:
```css
.d-flex { display: flex; }
.flex-row { flex-direction: row; }
.flex-column { flex-direction: column; }
.justify-content-center { justify-content: center; }
.align-items-center { align-items: center; }
.gap-1 { gap: 4px; }
.gap-2 { gap: 8px; }
.gap-3 { gap: 16px; }
```

**Display Utilities**:
```css
.d-none { display: none; }
.d-block { display: block; }
.d-inline { display: inline; }
.d-inline-block { display: inline-block; }
.d-flex { display: flex; }
.d-grid { display: grid; }
```

### 7. dashboard.css (100+ satır)

**Amaç**: Dashboard-spesifik komponentler

```css
.dashboard-header {
  padding: 24px;
  background: linear-gradient(135deg, #1a237e 0%, #283593 50%, #1565c0 100%);
  color: white;
  border-radius: 8px;
  margin-bottom: 24px;
}

.dashboard-header h1 {
  font-size: 28px;
  font-weight: 700;
  margin-bottom: 8px;
}

.dashboard-header p {
  opacity: 0.9;
  font-size: 14px;
}

.dashboard-card {
  background: white;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  transition: all 0.2s;
}

.dashboard-card:hover {
  box-shadow: 0 4px 16px rgba(0,0,0,0.12);
  transform: translateY(-2px);
}

.quick-access-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
}

.critical-info-card {
  background: #fff3e0;
  border-left: 4px solid #ff9800;
  padding: 16px;
  border-radius: 4px;
}

.sts-mobile-table {
  /* Mobile-responsive tablo */
}

.sts-mobile-stack {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
```

---

## 🎯 Tasarım Değişkenleri

### Kullanım Örneği

```css
/* Bir komponent tanımı */
.card {
  background: var(--gbl-white);
  border: 1px solid #ddd;
  border-radius: var(--gbl-radius-md);
  box-shadow: var(--gbl-shadow-sm);
  padding: var(--gbl-spacing-md);
  font-family: var(--gbl-font-family);
}

.card-header {
  color: var(--gbl-primary);
  font-size: var(--gbl-font-size-lg);
  font-weight: 600;
  margin-bottom: var(--gbl-spacing-md);
}
```

### Benefit'ler

- ✅ **Tutarlılık**: Renk ve boşluk tüm sitede aynı
- ✅ **Bakıma Uygunluk**: Bir yerde değişir, her yerde güncellenir
- ✅ **Tema Yönetimi**: Gelecekte dark mode kolay uygulanabilir
- ✅ **Tasarımcı-Dev İşbirliği**: Token sistemle iletişim daha net

---

## 🧩 Komponent Kütüphanesi

### Button Komponentleri

```html
<!-- Primary Button -->
<button class="btn btn-primary">Gönder</button>

<!-- Success Button -->
<button class="btn btn-success">Kaydet</button>

<!-- Danger Button -->
<button class="btn btn-danger">Sil</button>

<!-- Outline Button -->
<button class="btn btn-outline-secondary">İptal</button>
```

### Card Komponentleri

```html
<!-- Basic Card -->
<div class="card">
  <div class="card-header bg-light">
    <h5 class="mb-0">Başlık</h5>
  </div>
  <div class="card-body">
    İçerik buraya gelir
  </div>
</div>

<!-- Card with Badge -->
<div class="card">
  <div class="card-header d-flex justify-content-between align-items-center">
    <h5 class="mb-0">Başlık</h5>
    <span class="badge bg-primary">5 item</span>
  </div>
  <div class="card-body">
    İçerik
  </div>
</div>
```

### Alert Komponentleri

```html
<!-- Alert with dismiss button -->
<div class="alert alert-success alert-dismissible fade show" role="alert">
  <strong>Başarılı:</strong> İşlem tamamlandı
  <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
</div>

<!-- Alert with icon -->
<div class="alert alert-danger">
  <strong>❌ Hata:</strong> Bir şey yanlış gitti
</div>
```

### Badge Komponentleri

```html
<!-- Status Badges -->
<span class="badge bg-success">✓ Aktif</span>
<span class="badge bg-warning text-dark">⏳ Beklemede</span>
<span class="badge bg-danger">✗ Kapali</span>

<!-- Info Badges -->
<span class="badge bg-info">Bilgi</span>
<span class="badge bg-light text-dark">#123</span>
```

---

## 🛠️ Utility Sınıfları

### Yaygın Kullanım

```html
<!-- Spacing -->
<div class="p-3 m-2">Boşluk uygulanmış</div>
<div class="mt-4 mb-2">Üst ve alt marjin</div>

<!-- Text Alignment -->
<p class="text-center">Ortaya hizalı</p>
<p class="text-muted">Müteşekkir metin</p>

<!-- Flexbox -->
<div class="d-flex gap-2 justify-content-center">
  <button>Buton 1</button>
  <button>Buton 2</button>
</div>

<!-- Display -->
<div class="d-none d-md-block">Sadece masaüstünde görün</div>
```

---

## 📱 Responsive Tasarım

### Breakpoint'ler

```css
/* Mobile: < 576px */
/* Tablet: 576px - 768px */
/* Desktop: > 768px */
```

### Mobile-First Yaklaşım

```html
<!-- Mobilde tek sütun, desktop'ta iki sütun -->
<div class="row">
  <div class="col-12 col-md-6">Sütun 1</div>
  <div class="col-12 col-md-6">Sütun 2</div>
</div>
```

### Responsive Tablo

```html
<table class="table table-mobile-stack">
  <thead>
    <tr>
      <th>Ad</th>
      <th>E-posta</th>
      <th>Durum</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td data-label="Ad">John Doe</td>
      <td data-label="E-posta">john@example.com</td>
      <td data-label="Durum"><span class="badge bg-success">Aktif</span></td>
    </tr>
  </tbody>
</table>
```

---

## ✅ Modernize Edilen Views

### Tamamlanan (9 Dosya)

1. **STS Dashboard** - Kart-tabanlı layout
2. **STS Sevkiyat** - Modern filtre formu
3. **STS RaporSts/Index** - Tab-based raporlar
4. **STS RaporSts/DetailedRapor** - Detaylı rapor
5. **PRS Sikayet** - Şikayet yönetim listesi
6. **PRS ResmiTatil** - Tatil takvimi
7. **PRS Kullanici** - Kullanıcı listesi
8. **PRS Kullanici/Profil** - Profil kartları
9. **PRS Kullanici/Liste2** - Personel listesi

### Modernizasyon Adımları

```
1. Inline <style> blokları kaldır
2. Sayfaya container-fluid sarısını ekle
3. Header'a dashboard-header sınıfı ekle
4. Listeleri card'larla sar
5. Tablo stillerini güncelle
6. Emoji'ler ekle (👥 👤 📋 vb.)
7. Buton stillerini güncelle
8. Alert'ları modern yap
9. Build ve test et
```

---

## 🚧 Kalan Çalışmalar

### 4 Karmaşık View (2,282 satır)

1. **Kullanici/Yetkilendirme.cshtml** (286 satır)
   - Yetkilendirme tablosu
   - Checkbox grup seçimi
   - Teknikler: Inline stiller yoğun, JavaScript entegrasyonu

2. **HizliMesaiGirisi/Index.cshtml** (581 satır)
   - İnteraktif takvim
   - Dinamik form rendering
   - Teknikler: Karmaşık CSS grid, JavaScript event handling

3. **Mesai/Index.cshtml** (763 satır)
   - Mesai takip schedule
   - Satır/sütun etkileşimleri
   - Teknikler: Karmaşık layout, inline data bindings

4. **Puantaj/Index.cshtml** (655 satır)
   - Puantaj takvimi
   - Renkli durum hücreleri
   - Teknikler: Modal popup'lar, dinamik tablolar

### Tavsiyeler

- 💡 Her dosya ayrı oturumda ele alınabilir
- 💡 Inline CSS'i CSS dosyasına ekstraksiyon yapılmalı
- 💡 İnteraktif komponentler için yardımcı sınıflar oluştur
- 💡 JavaScript modülerleştirilmeli

---

## 📊 Proje İstatistikleri

| Metrik | Değer |
|--------|-------|
| Toplam CSS Satır | 2000+ |
| CSS Dosya Sayısı | 7 |
| Tasarım Değişkeni | 50+ |
| Komponent Sınıfı | 100+ |
| Utility Sınıfı | 700+ |
| Modernize Edilen View | 9 |
| Build Durumu | ✅ 0 Hata |
| Uyarı Sayısı | 46 (kabul edilebilir) |

---

## 🎓 Best Practices

### ✅ Yapılması Gerekenler

- Tasarım değişkenleri kullan
- Komponent sınıflarını tekrar kullan
- Utility sınıflarını kombinle
- Mobile-first yaklaş
- Inline stiller yerine CSS sınıfları kullan
- Semantik HTML kullan
- Bootstrap grid'i doğru kullan

### ❌ Yapılmaması Gerekenler

- Inline `style` attribute'u
- Hardcoded renkler ve boşluklar
- Tanımsız CSS sınıfları
- Responsive olmayan layout
- Erişilebilirlik kurallarını görmezden gel
- CSS dosyaları arasında dairesel import

---

**Belge Sürümü**: 1.0  
**Son Güncelleme**: 25 Mayıs 2026  
**Durum**: Aktif Geliştirme
