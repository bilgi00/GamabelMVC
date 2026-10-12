# Sevkiyat Filtre Sistemi — Özellik Dokümantasyonu

**Kapsam:** STS → Sevkiyat → Sevk Bekleyen Eksikler listesindeki Bölüm / Firma / Grup filtreleri
**Durum:** Güncel (12 Ekim 2026 itibarıyla kodla doğrulandı)
**İlgili CSS sistemi:** [CSS_DESIGN_SYSTEM.md](CSS_DESIGN_SYSTEM.md)

> Bu dosya yalnızca filtre sistemini anlatır. Genel değişiklik geçmişi için
> [IMPLEMENTATION_SUMMARY_TR.md](IMPLEMENTATION_SUMMARY_TR.md), teknik yapı için
> [TECHNICAL_DOCUMENTATION.md](TECHNICAL_DOCUMENTATION.md) dosyalarına bakın.

---

## Konum

| Katman | Dosya | Not |
|---|---|---|
| View | `Views/STS/Sevkiyat/SevkBekleyenEksikler.cshtml` | Filtre formu, badge'ler, tablo burada |
| View (menü) | `Views/STS/Sevkiyat/Index.cshtml` | Yalnızca yönlendirme kartları; filtre içermez |
| Controller | `Controllers/STS/SevkiyatController.cs` | `Index`, `SevkBekleyenEksikler`, `SevkEt`, `ExportBekleyenEksikler` |

> 12 Mayıs 2026 revizyonuyla buton metni "Güncelle" → **"Listele"** oldu.
> 25 Mayıs 2026'da filtre formu `Index` sayfasından `SevkBekleyenEksikler` sayfasına taşındı;
> `Index` artık iki karta (Sevk Bekleyen Eksikler / Geçmiş) yönlendiren bir menüdür.

## Filtreler ve Davranış

**Üç filtre:** Bölüm (`subeAdi`), Firma (`firma`), Grup (`grup`) — üçü de opsiyonel, boşsa tüm kayıtlar.

### 1. Manuel "Listele" butonu
Otomatik form submit yok; kullanıcı seçimini yapıp butona basar. Form içinde
`<input type="hidden" name="forceRefresh" value="true" />` bulunur.

### 2. Session tabanlı filtre hafızası
`forceRefresh=true` geldiğinde seçimler Session'a yazılır; üç parametre de boş gelirse
Session'dan geri yüklenir. Böylece F5 sonrası filtreler korunur.

Anahtarlar: `LastSubeFilter`, `LastFirmaFilter`, `LastGrupFilter`

### 3. Parametre temizleme ve null-safe sorgu değişkenleri
```csharp
subeAdi = subeAdi?.Trim();
firma   = firma?.Trim();
grup    = grup?.Trim();

// ViewBag için orijinal değer, SQL için null-safe kopya:
var subeAdiForQuery = string.IsNullOrWhiteSpace(subeAdi) ? null : subeAdi;
var firmaForQuery   = string.IsNullOrWhiteSpace(firma)   ? null : firma;
var grupForQuery    = string.IsNullOrWhiteSpace(grup)    ? null : grup;
```

### 4. SQL filtreleme (parametrik)
`u.Grup` sütunu `stk_Urun` üzerinden join ile gelir; grup dropdown'u
`SELECT DISTINCT Grup FROM stk_Urun WHERE Grup IS NOT NULL AND Grup != ''` ile doldurulur.

### 5. Görsel geri bildirim (badge)
Form altında yalnızca dolu olan filtreler için `badge bg-info` gösterilir:
`📍 Bölüm: …` / `🏢 Firma: …` / `📦 Grup: …`
Eski "Aktif Filtreler" alert kutusu 12 Mayıs revizyonunda kaldırıldı; geri bildirim tek yerde (badge) toplandı.

### 6. Dropdown seçim bağlama
Seçili değer `selected` attrübutü ile geri bağlanır; varsayılan açılışta
"Tüm Bölümler / Tüm Firmalar / Tüm Gruplar" seçili gelir (12 Mayıs düzeltmesi).

### 7. Navigasyonda filtre koruma
`SevkEt` işlemi sonrası dönüş:
```csharp
return RedirectToAction("SevkBekleyenEksikler", new {
    subeAdi = lastSube, firma = lastFirma, grup = lastGrup, forceRefresh = "true"
});
```

### 8. Excel çıktısı
"Excel" butonu `ExportBekleyenEksikler` action'ına üç filtreyi de taşır
(`asp-route-subeAdi / firma / grup`); dosya EPPlus 7.0.7 ile üretilir.

## Kullanıcı Akışı

```
Sayfa yüklenir → Session'da filtre varsa otomatik restore
→ Kullanıcı Bölüm/Firma/Grup seçer → badge'ler anında görünür
→ "Listele" → forceRefresh=true → Session'a yazılır, liste filtrelenir
→ F5 → filtreler Session'dan geri gelir
→ "Sevk Et" → filtreler korunarak listeye dönülür
→ "Excel" → filtrelenmiş veri indirilir
```

## Test Adımları

1. `/Account/StsLogin` — STS kullanıcısıyla giriş (DepoSorumlusu veya Admin)
2. `Sevkiyat → Sevk Bekleyen Eksikler`
3. Üç filtre seçin → badge'ler görünsün
4. "Listele" → liste ve tablo filtrelenmiş gelsin, tabloda Grup sütunu olsun
5. F5 → filtreler restore olmalı
6. "Sevk Et" → dönüşte filtreler korunmalı
7. "Excel" → yalnızca filtrelenmiş kayıtlar inmeli

## Bilinen Sınırlar

- Filtre hafızası Session'dadır; oturum kapanınca sıfırlanır (kalıcı "favori filtre" yok).
- Sevkiyat Geçmişi/Onay Süreci tablosunda `Onaylandi` durumundaki kayıtlar listede gösterilmez (12 Mayıs kararı).

## Geçmiş Sürüm Notları (kısaltılmış)

| Tarih | Özet |
|---|---|
| 12 May 2026 | "Listele" metni; dropdown varsayılan seçim; uyarı modal'a alındı; Onaylandı kayıtları listeden çıkarıldı |
| 14 May 2026 | Grup filtresi eklendi (dropdown + tablo sütunu + session anahtarı) |
| 25 May 2026 | Filtre formu `SevkBekleyenEksikler` sayfasına taşındı; CSS sistemiyle uyum |

---
**Son doğrulama:** 12 Ekim 2026 (kod üzerinden: `SevkiyatController.cs`, `SevkBekleyenEksikler.cshtml`)
