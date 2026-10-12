# GamabelMVC – Yeni Sayfa UI Standardı

**Amaç:** GamabelMVC projesinde oluşturulacak yeni Razor (`.cshtml`) sayfalarının, Hızlı Mesai Girişi örneğindeki gibi tutarlı, sade, okunabilir ve Bootstrap CDN tabanlı bir görünüme sahip olmasını sağlamak.

## 1. Temel kurallar

- Mevcut ASP.NET Core MVC / Razor yapısını, controller'ları, modelleri ve iş mantığını koru.
- Öncelik: **Bootstrap CDN ve standart kütüphane sınıfları**. Görünüm için mümkün olduğunca özel CSS yazma.
- Sayfa içinde uzun `<style>...</style>` blokları, sayfaya özel CSS dosyaları ve gereksiz inline `style=""` kullanma.
- Mevcut `_Layout.cshtml` Bootstrap veya ikon kütüphanelerini zaten yüklüyorsa aynı CDN'leri her sayfada tekrar ekleme. Önce `_Layout.cshtml` dosyasını kontrol et.
- Sayfanın işlevini değiştirme; yalnızca istenen arayüz düzenlemesini yap. Form alanlarının `name`, `id`, `asp-for`, `asp-action`, doğrulama özellikleri ve JavaScript bağlantıları korunmalı.
- Projede kullanılan Bootstrap sürümüyle uyumlu sınıflar kullan. Projede Bootstrap 5.3.8 standardı benimsenmişse aynı sürümü kullan.
- Mobil ve masaüstü uyumluluğu için Bootstrap grid ve responsive yardımcı sınıflarını kullan.

## 2. Önerilen CDN standardı

Önce `_Layout.cshtml` içinde bu kütüphanelerin yüklü olup olmadığını kontrol et. Zaten yükleniyorlarsa sayfada tekrar ekleme. Gerekliyse merkezi olarak `_Layout.cshtml` içine ekle.

### Bootstrap 5.3.8 CSS

```html
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css"
      rel="stylesheet"
      integrity="sha384-some-valid-hash"
      crossorigin="anonymous">
```

> **Önemli:** Yukarıdaki `integrity` değeri örnek yer tutucudur; doğrulanmış bir SRI hash değildir. Gerçek hash kullanılmayacaksa `integrity` satırını kaldır. Tercihen Bootstrap'ın resmî CDN dokümantasyonundaki güncel ve doğrulanmış URL/hash çiftini kullan.

### Bootstrap 5.3.8 JavaScript bundle

```html
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js"
        crossorigin="anonymous"></script>
```

### Font Awesome 6.5.1 (isteğe bağlı)

```html
<link rel="stylesheet"
      href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css">
```

İkonlar gerekiyorsa Font Awesome kullan; aynı projede farklı ikon kütüphanelerini karıştırma. CDN'leri her `.cshtml` sayfasına ayrı ayrı koymak yerine mümkünse `_Layout.cshtml` içinde merkezi yönet.

## 3. Sayfa iskeleti

Yeni bir Razor sayfası oluştururken mevcut layout'u kullan:

```cshtml
@{
    ViewData["Title"] = "Sayfa Başlığı";
    Layout = "_Layout";
}

<div class="container-fluid py-3">
    <div class="card shadow-sm rounded-4 mb-3">
        <div class="card-body d-flex flex-column flex-lg-row align-items-lg-center justify-content-between gap-3">
            <div>
                <h1 class="h4 fw-bold text-primary mb-1">
                    <i class="fa-solid fa-bolt me-2"></i>Sayfa Başlığı
                </h1>
                <p class="text-secondary mb-0">Sayfanın kısa açıklaması.</p>
            </div>

            <div class="d-flex flex-wrap gap-2">
                <button type="button" class="btn btn-outline-primary">
                    <i class="fa-solid fa-plus me-1"></i> Yeni Kayıt
                </button>
                <button type="submit" class="btn btn-success">
                    <i class="fa-solid fa-check me-1"></i> Kaydet
                </button>
            </div>
        </div>
    </div>

    <div class="card shadow-sm rounded-4 mb-3">
        <div class="card-body">
            <!-- Arama ve filtre alanları -->
            <div class="row g-3 align-items-end">
                <div class="col-12 col-md-4 col-xl-3">
                    <label for="arama" class="form-label">Arama</label>
                    <input id="arama" name="arama" type="search"
                           class="form-control"
                           placeholder="Aranacak metni yazın...">
                </div>
                <div class="col-12 col-md-4 col-xl-3">
                    <label for="filtre" class="form-label">Filtre</label>
                    <select id="filtre" name="filtre" class="form-select">
                        <option value="">Tümü</option>
                    </select>
                </div>
                <div class="col-12 col-md-4 col-xl-3">
                    <button type="button" class="btn btn-primary w-100">
                        <i class="fa-solid fa-magnifying-glass me-1"></i> Ara
                    </button>
                </div>
            </div>
        </div>
    </div>

    <div class="card shadow-sm rounded-4">
        <div class="card-body">
            <h2 class="h5 fw-bold mb-3">İçerik Başlığı</h2>
            <div class="table-responsive">
                <table class="table table-hover table-striped align-middle mb-0">
                    <thead class="table-light">
                        <tr>
                            <th scope="col">#</th>
                            <th scope="col">Ad</th>
                            <th scope="col">Durum</th>
                            <th scope="col" class="text-end">İşlem</th>
                        </tr>
                    </thead>
                    <tbody>
                        <!-- Razor foreach ile satırları üret -->
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>
```

> Bu iskelet bir örnektir. Kullanılmayan butonları/alanları kaldır; işlevi olmayan kontroller bırakma.

## 4. Görsel tasarım kuralları

- **Sayfa başlığı:** `card`, `rounded-4`, `shadow-sm`, `text-primary`, `fw-bold` sınıflarıyla belirgin ama sade.
- **Açıklama:** `text-secondary` ve kısa bir metin.
- **Aksiyonlar:** `btn`, `btn-primary`, `btn-outline-primary`, `btn-success`, `btn-outline-secondary` ve `gap-2`.
- **Arama/filtre:** `form-control`, `form-select`, `input-group`, `form-label`.
- **Tablolar:** `table`, `table-hover`, gerektiğinde `table-striped`, `align-middle`, `table-responsive`.
- **Rozetler:** `badge text-bg-success`, `badge text-bg-warning`, `badge text-bg-danger`, `badge text-bg-secondary`.
- **Uyarılar:** `alert alert-success`, `alert alert-warning`, `alert alert-danger`, `alert alert-info`.
- **Yerleşim:** `container-fluid`, `row`, `col-*`, `d-flex`, `flex-wrap`, `justify-content-between`, `align-items-center`, `gap-*`.
- **Boşluklar:** `p-*`, `m-*`, `py-*`, `mb-*` gibi Bootstrap yardımcı sınıfları.
- Çok fazla renk, kalın çerçeve, animasyon veya gölge kullanma. Beyaz/açık arka plan, mavi vurgu ve gerektiğinde yeşil kaydet butonu tercih et.
- Uzun tabloları `table-responsive` içinde göster. Sabit kolon veya özel kaydırma gerekiyorsa önce Bootstrap çözümünü dene; özel CSS ancak zorunluysa ve gerekçesi belirtilerek kullanılmalı.

## 5. Razor ve CSS hatalarından kaçınma

- Razor içinde CSS at-rule kullanmak gerekiyorsa `@media` Razor tarafından C# ifadesi gibi algılanabilir. Mümkünse özel CSS kullanma; zorunluysa Razor içinde `@@media` yaz veya CSS'yi harici dosyaya taşı.
- `@model`, `@foreach`, `@if`, `asp-for`, `asp-action` gibi Razor ifadelerini doğru bağlamda kullan.
- `Layout = "_Layout";` yazımı, projedeki mevcut layout dosyasının adıyla uyuşmalıdır. Projede kullanılan mevcut tanımı bozma.
- `@section Scripts { ... }` kullanılıyorsa `_Layout.cshtml` içinde `@RenderSection("Scripts", required: false)` bulunduğunu doğrula.
- JavaScript kodlarını tercihen `@section Scripts` içine koy; jQuery/Bootstrap bağımlılıklarının yüklenme sırasını kontrol et.
- Formlarda anti-forgery ve sunucu tarafı doğrulama davranışını koru.

## 6. Erişilebilirlik ve kullanım kolaylığı

- Her input için görünür ve doğru `label` kullan.
- Butonların işlevini açık yaz; yalnızca ikonla bırakma (gerekli durumlarda `aria-label` ekle).
- Renkleri tek başına durum belirtmek için kullanma; metin veya ikon da ekle.
- Klavye ile erişimi ve görünür odak durumlarını engelleme.
- Mobilde butonları `flex-wrap` ile alt satıra geçirebil.
- Uzun metinler ve tablolar küçük ekranda taşmamalı.

## 7. Yeni sayfa üretirken uygulanacak kontrol listesi

- [ ] Önce ilgili Controller, Model, mevcut View ve `_Layout.cshtml` incelendi.
- [ ] Mevcut iş mantığı ve route/parametre adları korundu.
- [ ] Bootstrap CDN zaten layout'ta varsa tekrar eklenmedi.
- [ ] Sayfaya özel `<style>` bloğu ve gereksiz `style=""` kullanılmadı.
- [ ] Standart Bootstrap sınıflarıyla kart, form, tablo ve butonlar düzenlendi.
- [ ] Responsive grid kullanıldı.
- [ ] Razor ifadeleri ve `@media` kaynaklı derleme hataları kontrol edildi.
- [ ] JavaScript ve `@section Scripts` yükleme sırası kontrol edildi.
- [ ] Form doğrulama ve güvenlik davranışı korundu.
- [ ] Kullanılmayan veya işlevsiz arayüz öğeleri bırakılmadı.

## 8. Kod üretim talimatı

Yeni sayfa kodu üretirken şu talimatı uygula:

> “GamabelMVC projesinin mevcut ASP.NET Core MVC/Razor mimarisini koru. Önce `_Layout.cshtml` içindeki CDN ve script yüklemelerini kontrol et. Görünümü mümkün olduğunca Bootstrap 5.3.8 standart sınıfları ve gerekiyorsa Font Awesome 6.5.1 ile oluştur. Sayfa içinde özel CSS (`<style>` veya gereksiz `style=""`) yazma; yalnızca Bootstrap sınıflarını kullan. Mevcut controller, model, route, form alanı, doğrulama ve JavaScript işlevlerini bozma. Responsive, sade, açık arka planlı, mavi vurgulu ve okunabilir bir tasarım üret. Çıktıda tam `.cshtml` kodunu ve gerekiyorsa gerekli CDN değişikliklerini ayrı ayrı ver.”

---

**Not:** Bu standart, Bootstrap yardımcı sınıflarıyla yapılabilecek tasarımlar için geçerlidir. Gerçekten özel davranış gerektiren sabit kolon, karmaşık takvim hücresi veya özel yazdırma düzeni gibi durumlarda önce standart çözüm değerlendirilmeli; özel CSS gerekiyorsa gerekçesi ve etkisi açıklanmalıdır.
