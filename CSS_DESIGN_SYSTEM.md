# CSS Tasarım Sistemi Dokümantasyonu

**Son güncelleme:** 12 Ekim 2026 (v3.3 — Genel Tasarım Standardı, HizliMesaiGirisi hm- ölçülerine göre eşitlendi)
**Durum:** Güncel — gerçek dosya yapısıyla doğrulandı

---

## Genel Tasarım Standardı (v3.3) — Tüm Sayfalar İçin Tek Format

**Temel:** Bootstrap **5.3.8 (CDN)** — `Views/Shared/_Layout.cshtml`'den yüklenir
(`cdn.jsdelivr.net/npm/bootstrap@5.3.8`). Font: Bootstrap body font stack; özel renkler
aşağıdaki tokenlerle. Hiçbir sayfada ayrı font/theme CDN'i kullanılmaz.

Bu standart `Views/PRS/Puantaj/Index.cshtml` ölçüleri baz alınarak hazırlanmıştır ve
**tüm PRS/STS sayfalarında** uygulanacaktır. CSS tam karşılıkları `site.css` →
components bölümündedür ("GENEL TASARIM" bloğu).

### Tasarım Kalıpları (tokenler)

| Kalıp | Değer | Nerede |
|---|---|---|
| Kart | beyaz zemin, `1px solid #e1e6ef`, radius **16px**, hafif gölge, alt boşluk 16px | `.sayfa-karti` |
| Başlık (kart içi) | 18px / 700 / `#1f2937` | `.kart-baslik` |
| Alt açıklama | 13px / `#5b6678` | `.kart-alt-yazi` |
| Kontrol yüksekliği | **38px** (select, input, sabit alan, ok, ay etiketi) | filtre standartı |
| Kenarlık / radius (kontroller) | `1px solid #cbd5e1` / **10px** | filtre standartı |
| Kontrol metni | 14px / 500 / `#1f2937` | filtre standartı |
| Vurgu mavisi (başlık/ikon/ok) | `#1e3a8a` · `#2563eb` | `page-header-*`, `.takvim-ok` |
| Filtre çubuğu | flex, gap **10px**, padding `10px 16px` | `.filtre-cubugu` |

### Bileşen Sınıfları

**1) Üst başlık kartı** — elle HTML yazmak yerine TagHelper kullanın:

```cshtml
<sayfa-baslik baslik="Sayfa Adı" aciklama="Kısa açıklama." ikon="fa-calendar-alt">
    <button type="button" class="btn btn-sm btn-primary" id="...">Birincil</button>
    <button type="button" class="btn btn-sm btn-danger" id="...">PDF</button>
</sayfa-baslik>
```
Ürettiği yapı: `.page-header-card` (sol: ikon+başlık+açıklama; sağ: `no-print` aksiyonlar).

**2) Kart:** `<section class="sayfa-karti">` — alt bölümler için ortak kap.
Başlıklı kart: `<div class="kart-basligi">` + `<h2 class="kart-baslik">` + `<p class="kart-alt-yazi">`.

**3) Filtre çubuğu:** kartın içine `<section class="sayfa-karti filtre-cubugu">`:
- Kontroller: `form-select` / `form-control` (Bootstrap) + genişlik sınıfı:
  `.filtre-alan` (1:1:180px) · `.filtre-alan-genis` (arama kutusu, 2:1:230px)
  *(ölçüler HizliMesaiGirisi `hm-filter-unit`/`hm-filter-search` değerleridir — 12 Eki 2026 v3.3)*
- Değişmeyen değer: `<span class="filtre-sabit filtre-alan">`
- İkonlu arama kutusu: `<div class="input-group filtre-alan-genis"><span class="input-group-text"><i class="bi bi-search"></i></span><input class="form-control" ... /></div>` — ikon 40px, yükseklik 38px, radius `10px 0 0 10px` / `0 10px 10px 0`
- Çubuktaki butonlar: `.filtre-cubugu .btn` otomatik 38px yükseklik + 10px radius alır
- Satır uyarısı/mesajı: `<div class="filtre-mesaj">…</div>` (tüm satırı kaplar, sağa hizalı)
- Dönem gezinme:
```html
<div class="takvim-gezinme" role="group" aria-label="Ay ve yıl">
    <button type="button" class="takvim-ok" id="previousMonthBtn" aria-label="Önceki ay">←</button>
    <span class="takvim-ay" id="monthTitle">Mart 2026</span>
    <button type="button" class="takvim-ok" id="nextMonthBtn" aria-label="Sonraki ay">→</button>
</div>
```

### Kurallar

1. Yeni sayfada kart/filtre/takvim gezinme **elle ve inline CSS ile yazılmaz**; bu sınıflar kullanılır.
2. Sayfaya özel kalan kısımlar (tablo, lejant, özel renkler) sayfanın kendi `<style>` bloğunda, **genel sınıfların ölçülerini tekrar etmeden** yazılır.
3. Butonlar Bootstrap varyantlarıdır (`btn-sm` + `btn-primary/btn-danger/btn-warning text-dark`); butonlarda `id` zorunludur (JS bağlanır).
4. Yazdırılmayan öğelere `no-print` eklenir (aksiyon çubuğu otomatik ekler).
5. Ölçü değişikliği gerekirse **buradan** (site.css genel blok) yapılır; sayfa kopyaları değiştirilmez.

### Uygulanan Sayfalar

- ✅ `Views/PRS/Puantaj/Index.cshtml` (12 Eki 2026) — sayfa-özel filtre CSS'leri silindi, genel sınıflara geçti
- ✅ `Views/PRS/Mesai/Index.cshtml` (12 Eki 2026) — `mesai-header`/`mesai-controls` silindi; filtre çubuğu `.sayfa-karti.filtre-cubugu`, tablo+footer `.sayfa-karti` kartına alındı
- ⏳ Sıradakiler: `Views/PRS/Mesai/HizliMesaiGirisi/Index.cshtml`, STS sayfaları

---

## Dosya Yapısı (ÖNEMLİ)

Tasarım sistemi **tek dosyada**, bölüm yorumlarıyla organize edilmiştir:

```
wwwroot/css/
└── site.css   (3804 satır, tek dosya)
```

`site.css` içindeki bölüm sırası ve başlangıç satırları:

| Sıra | Bölüm | Başlangıç satırı | İçerik |
|---|---|---|---|
| 1 | `/* ===== variables.css ===== */` | 3 | Tasarım tokenleri (renk, boşluk, tipografi, gölge) |
| 2 | `/* ===== components.css ===== */` | 145 | Yeniden kullanılabilir komponentler |
| 3 | `/* ===== layout.css ===== */` | 582 | Sayfa yapısı, navbar, scrollbar |
| 4 | `/* ===== forms.css ===== */` | 1069 | Form kontrolleri, `form-horizontal-3col` |
| 5 | `/* ===== tables.css ===== */` | 1464 | Tablo stilleri, mobil yığın |
| 6 | `/* ===== dashboard.css ===== */` | 1892 | `dashboard-header`, `quick-access-section` |
| 7 | `/* ===== utilities.css ===== */` | 2280 | Tekil amaçlı yardımcı sınıflar |

> **Eski notun düzeltilmesi:** İlk sürümde bu bölümler ayrı `.css` dosyaları olarak
> (`variables.css`, `components.css` …) planlanmıştı. Uygulamada tek `site.css` içinde
> birleştirildi; `@import` zinciri **yoktur**. Bölüm sırası yine önemlidir:
> değişkenler → komponentler → yapı → formlar → tablolar → dashboard → utility.

Layout'a yükleniş: `Views/Shared/_Layout.cshtml` → `~/css/site.css` (asp-append-version).

## Temel Tasarım Tokenleri (variables bölümü)

```css
/* Renkler */
--gbl-primary: #283593;        /* koyu mavi */
--gbl-secondary: #1565c0;      /* açık mavi */
--gbl-danger-light: #ffebee;
--gbl-warning: #fff3e0;

/* Boşluk ölçeği */
--gbl-spacing-xs: 4px;  --gbl-spacing-sm: 8px;
--gbl-spacing-md: 16px; --gbl-spacing-lg: 24px;
--gbl-spacing-xl: 32px; --gbl-spacing-2xl: 48px; --gbl-spacing-3xl: 64px;

/* Tipografi */
--gbl-font-family: 'Segoe UI', Tahoma, Geneva, sans-serif;
--gbl-font-size-xs: 11px; … --gbl-font-size-3xl: 28px;

/* Gölge ölçeği */
--gbl-shadow-xs … --gbl-shadow-xl
```

Yeni stil eklerken **hardcoded renk/boşluk yerine bu değişkenleri** kullanın.

## Sık Kullanılan Komponentler

| Sınıf | Amaç |
|---|---|
| `page-header-card` | **Sayfa üst başlığı (standart, `<sayfa-baslik>` TagHelper)** — başlık + açıklama + aksiyon butonları |
| `form-horizontal-3col` | Filtre formlarında 3 kolonlu yatay düzen (responsive kırılımlı) |
| `dashboard-header` | Dashboard sayfa üstü kurumsal başlık bandı (gradient) |
| `quick-access-section` | Dashboard hızlı erişim buton alanı (ortalanmış, flex-wrap) |
| `sts-mobile-table` / `data-label` | Mobilde tabloyu yığına çeviren yapı |
| `.table-hover`, `.table-light` | Bootstrap ile uyumlu tablo varyantları |
| `.badge.bg-info` | Aktif filtre göstergeleri |

## Sayfa Üst Başlığı — Standart Taslak (v3.1)

**Bileşen:** `<sayfa-baslik>` TagHelper (`TagHelpers/SayfaBaslikTagHelper.cs`)
**CSS:** `site.css` → components bölümü → `page-header-*`
**Ekran görünümü:** Sol blokta ikon + mavi başlık + gri açıklama; sağ blokta buton grubu; beyaz kart, 16px radius, ince çerçeve.

### Kullanım (tek yol — elle HTML yazmayın)

```cshtml
<sayfa-baslik baslik="Puantaj Takvimi"
              aciklama="Personel çalışma ve izin durumlarını aylık olarak görüntüleyin."
              ikon="fa-calendar-alt">
    <button type="button" class="btn btn-sm btn-warning text-dark" id="batchIzinBtn">Toplu İzin</button>
    <button type="button" class="btn btn-sm btn-primary" id="previewBtn">Önizleme (Yatay)</button>
    <button type="button" class="btn btn-sm btn-danger" id="pdfExportBtn">PDF</button>
</sayfa-baslik>
```

### Kurallar

1. **Parametreler:** `baslik` (zorunlu), `aciklama` (opsiyonel), `ikon` (Font Awesome adı, opsiyonel).
2. **Aksiyon alanı** etiketin içine yazılan içerikten üretilir; otomatik `no-print` olur (yazdırılan/PDF çıktısında görünmez).
3. **Butonlar** Bootstrap standardında: `btn btn-sm btn-primary` (birincil aksiyon), `btn-danger` (yıkıcı/PDF), `btn-warning text-dark` (araç). `id` zorunlu — JS dinleyicileri buna bağlanır.
4. İçerik verilmezse sağ blok render edilmez (salt başlık kartı).
5. Yeni bir sayfada **aynı görünümlü üst kısım elle div/inline CSS ile yazılmaz**; bu bileşen kullanılır. Farklılaşma gerekiyorsa bileşen genişletilir.

### Uygulanan sayfalar

- `Views/PRS/Puantaj/Index.cshtml` (12 Eki 2026) — Toplu İzin / Önizleme (Yatay) / PDF
- `Views/PRS/Mesai/Index.cshtml` (12 Eki 2026) — Satır Ekle (Tüm/Birim) / Önizleme / Excel / HTML / PDF

> Eski `puantaj-header pz-head` + `page-headline/page-sub` düzeni bu bileşenle değiştirildi; o sınıflar Puantaj sayfasından silindi. Mesai/Index'te karşılığı olan `mesai-header`/`mesai-controls` da silindi (12 Eki 2026). Kalan sayfalar (HizliMesaiGirisi) taşındığında aynı bileşene geçmelidir.

## Modernize Edilmiş Viewlar (9)

1. STS Dashboard (`STS/Home/Index`)
2. STS Sevkiyat (menü + SevkBekleyenEksikler)
3. STS RaporSts/Index
4. STS RaporSts/DetailedRapor
5. PRS Sikayet
6. PRS ResmiTatil
7. PRS Kullanici
8. PRS Kullanici/Profil
9. PRS Kullanici/Liste2

### Modernizasyon adımları (yeni sayfalar için de geçerli)

1. Inline `<style>` bloklarını `site.css`'e taşı (ilgili bölüme ekleyin)
2. Sayfayı `container-fluid` ile sarın
3. Başlığa `dashboard-header` ekleyin
4. Listeleri `card` içine alın
5. Buton/alert/badge'lerde Bootstrap utility + `gbl-*` değişkenlerini kullanın

## Kalan Çalışmalar (12 Ekim 2026 itibarıyla)

Hâlâ yoğun inline stil / karmaşık JS içeren 4 büyük view:

| Dosya | Satır | Not |
|---|---|---|
| `Views/PRS/Kullanici/Yetkilendirme.cshtml` | 288 | Checkbox grup mantığı |
| `Views/PRS/Mesai/HizliMesaiGirisi/Index.cshtml` | 1561 | İnteraktif takvim, büyümüş |
| `Views/PRS/Mesai/Index.cshtml` | 612 | FastReport standardına geçti (12 Eki); genel tasarım standardına geçti (12 Eki), tablo/imza stilleri sayfa-özel kaldı |
| `Views/PRS/Puantaj/Index.cshtml` | 1055 | Renkli takvim hücreleri, modal'lar |

> Eski dokümanda bu listede Mesai 763 / Puantaj 655 / HizliMesai 581 satırdı;
> güncel değerler yukarıdadır (HizliMesaiGirisi ciddi biçimde büyümüştür —
> bir sonraki refaktör dalgasında önce bu dosya ele alınmalıdır).

## Yapılması Gerekenler / Yapılmaması Gerekenler

**✅ Yapılmalı:**
- `var(--gbl-*)` tokenleri kullanın
- Yeni komponenti ilgili bölüm yorumunun altına ekleyin
- Mobil öncelikli responsive kalıpları (`data-label`, `sts-mobile-table`) kullanın

**❌ Yapılmamalı:**
- Inline `style="…"` attribute'ları (mevcut 4 view dışında yeniye yazmayın)
- Hardcoded renk/boşluk
- Tanımsız sınıf adları
- Bootstrap ile çakışan yeniden tanımlar

---
**Sürüm:** 1.1 (tek dosya yapısı gerçeğiyle güncellendi)
**İlgili:** [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) · [IMPLEMENTATION_SUMMARY_TR.md](IMPLEMENTATION_SUMMARY_TR.md)
