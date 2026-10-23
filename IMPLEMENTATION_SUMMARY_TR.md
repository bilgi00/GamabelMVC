# Uygulama Özeti ve Değişiklik Günlüğü (TR)

**Son güncelleme:** 23 Ekim 2026
**Bu dosya ne işe yarar:** Projeye tarih sırasıyla yapılan büyük değişikliklerin
özet kaydıdır. Ayrıntılar ilgili uzman dokümanlara yönlendirir.

> **Not (12 Ekim 2026):** Eski sürümdeki kronolojik uzun anlatılar (aynı özelliğin
> farklı tarihlerde tekrar anlatımı, kod satır numarası özetleri) kısaltıldı;
> yinelenen Sevkiyat filtre anlatımı [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md)'e,
> CSS mimarisi [CSS_DESIGN_SYSTEM.md](CSS_DESIGN_SYSTEM.md)'e taşındı.

---

## 🟢 Güncel Durum (23 Ekim 2026)

### Oturum süresi ve Puantaj güncellemeleri

- Oturum süresi dolduğunda PRS/STS modülüne uygun giriş ekranına yönlendirme yapılır. Sekme görünürlüğü, pencere odağı ve sayfanın yeniden gösterilmesi oturum sayacını tekrar kontrol eder.
- Puantaj eski durum kodlarını (`T`, `ÇH`, `H`, `ÖDİ`) güncel kodlara normalize eder (`X`, `ÇR`, `R`, `Öİ`). Ekran ve rapordaki durum/sayaç hesapları aynı kodları kullanır.
- Puantaj ekranı ve raporunda `İ` izin sayacı ile `Ö` ödeneksiz izin sayacı ayrılmıştır. Hakediş `30 - Öİ gün sayısı - PT gün sayısı` olarak hesaplanır.
- PDF raporunda “Hastalık” sayacı “Ö (ödeneksiz izin)” olarak değiştirilmiş ve imza alanı Birim Sorumlusu ile sınırlandırılmıştır.
- **PT - Part Time**, günlük durum seçeneklerine eklenmiştir; günleri `İ` sayacına dahil edilir ve hakedişten düşülür.
- Tasarım zamanı derlemesinde Git commit bilgisi komutu çalıştırılmaz; `System.Security.Cryptography.Pkcs` proje bağımlılıklarına eklenmiştir.
- Ayrıntılar: [SAYFA_GUVENLIK_KURALLARI.md](SAYFA_GUVENLIK_KURALLARI.md) ve uygulama içi Dokümantasyon sayfası.

### PRS Mesai → FastReport A4 Rapor Standardı (v3)

**Değişen yaklaşım:** Sayfadan serbest kaydetme/excel/pdf üretimi kaldırıldı;
tüm çıktılar tek standarta bağlandı ([FASTREPORT_A4_STANDART.md](FASTREPORT_A4_STANDART.md)).

- **Yeni uçlar:** `GET /Mesai/Rapor` (önizleme) ve `GET /Mesai/RaporPdf` (PDF indir)
- **Yeni şablon:** `Reports/Mesai/Mesai.Yatay.frx` — DoublePass, sürüm parametresi,
  `<Total>` toplamları, tekrarlayan başlık, `Sayfa [Page#] / [TotalPages#]`
- **Yeni katman:** `Services/Raporlama/` — `RaporServisi` + `RaporTanimi` kayıtları
  (yeni rapor = `RaporKaydi`'na tek satır; Önizleme ve PDF **aynı .frx**'den üretilir)
- **Yeni önizleme:** `Views/Shared/Rapor.cshtml` (yön seçici + PDF indir)
- **Kaldırılan uçlar:** `FastReportPreview`, `FastReportPdf`, `FastReportExcel`
- **Kaldırılan dosyalar:** `Reports/Mesai.frx`, `Reports/Mesai/EkMesaiCizelgesi.frx`,
  `Reports/Mesai/EkMesaiPersonelBazli.frx`, `Views/PRS/Mesai/FastReportPreview.cshtml`
- **Kaldırılan butonlar (Mesai/Index):** Kaydet, Excel (Yatay), PDF (Yatay/Personel)
- Excel çıktısı standart uyarınca kapatıldı (Excel .frx'ten üretilmez)

### Açılış ekranı

- `Views/Home/Index.cshtml`: Karşılama kartındaki "Puantaj Takvimine Git" butonu kaldırıldı.

---

## 📜 Geçmiş Değişiklik Özeti (kısaltılmış)

| Tarih | Başlık | Özet | Ayrıntı |
|---|---|---|---|
| 23 Eki 2026 | Oturum + Puantaj güncellemeleri | Modüle uygun oturum süresi dolum yönlendirmesi; Puantaj durum kodu normalizasyonu, `İ`/`Ö` sayaçları, hakediş ve rapor imza güncellemesi; PT durumu | Yukarıdaki güncel durum; [SAYFA_GUVENLIK_KURALLARI.md](SAYFA_GUVENLIK_KURALLARI.md) |
| 10 Tem 2026 | Mesai PDF akışı + kayıt/giriş düzeltmeleri | `printBirimPdf`/`printPersonelPdf` ile sayfa içi yazdırma; `aktif_mi=1` ile kayıt sonrası giriş sorunu çözüldü | Bu akış 12 Eki'de **kaldırıldı**; yerine FastReport standardı geldi (yukarıda). Giriş düzeltmesi hâlâ geçerli. |
| 25 May 2026 | CSS standardizasyonu + yapı modülerleştirme | Controllers/Models/Views `PRS`/`STS` alt klasörlerine ayrıldı; `site.css` tek dosyada 7 bölüme organize edildi | [CSS_DESIGN_SYSTEM.md](CSS_DESIGN_SYSTEM.md) |
| 25 May 2026 | Sevkiyat filtre formu taşınması | Filtreler `Index`'ten `SevkBekleyenEksikler`'e taşındı | [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) |
| 14 May 2026 | Grup filtresi + Dashboard UI | Sevkiyata Grup filtresi (dropdown+sütun+session); dashboard butonları ortalandı, kartlar kompaktlaştı | [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) |
| 12 May 2026 | Sevkiyat filtre revizyonu | "Listele" metni; dropdown varsayılan seçim; onaylı kayıtlar listeden çıkarıldı | [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) |
| 5–6 Haz 2026 | Admin yedekleme/geri yükleme + ürün toplu yükleme | Tablo seçmeli yedekleme; Excel ön izleme + uygula akışı; barkod çakışmalarında yumuşak hata | (Bu dosyanın eski geniş sürümünden kısaltıldı; ayrıntı git geçmişinde) |

### 5–6 Haziran 2026 ayrıntı özeti

1. **Admin — Tablo Seçmeli Yedekleme/Geri Yükleme:** Veritabanı tablolarının
   seçilerek yedeklenip geri yüklenebilmesi (AdminController).
2. **STS Ürün Toplu Yükleme:** Excel yüklemede "Ön İzleme → Uygula" iki aşamalı akış;
   barkod çakışmalarında işlemi bloklamayan uyarı yaklaşımı.

### 10 Temmuz 2026 ayrıntı özeti

1. **PDF buton/yazdırma akışı** — 12 Ekim 2026 itibarıyla geçersiz: popup engeline
   karşı geliştirilen `printBirimPdf`/`printPersonelPdf`/geçici yazdırma konteyneri
   akışı kaldırıldı; yerine sunucu tarafı FastReport üretimi geldi. Personel bazlı
   ayrı PDF kaldırıldı (tek birim bazlı rapor).
2. **Kayıt sonrası giriş** — `AccountController` register insert'ü `aktif_mi = 1`
   olarak güncellendi (geçerli ve sürüyor).

---

## Bilinen Açıklar (hâlâ geçerli)

- ❌ Area routing yok (route'lar düz `[controller]/[action]`)
- ❌ Unit test yok
- ❌ CSRF koruması yok
- ❌ Şifre hash'leme yok (düz metin karşılaştırma) — **kritik**
- ❌ Dosya/merkezi logging yok

> Güvenlik açıkları büyükten küçüğe: şifre hash'leme → CSRF → logging.
> Bağlantı stringi/SMTP kimlik bilgileri `appsettings.json`'da düz metindir;
> kaynak kontrolüne girmemelidir (bkz. `PROJE_INCELEME_RAPORU.md`).

## Önerilen Sıradaki Adımlar

| Faz | İş | Süre (tahmini) |
|---|---|---|
| 1 — Güvenlik | Şifre hash'leme (BCrypt), User Secrets'a geçiş, logging | ~1 hafta |
| 2 — Kalite | Pagination, ViewModel/Repository pattern, unit test | 2–3 hafta |
| 3 — Performans | Sorgu optimizasyonu, cache, DB index | 1+ ay |

---

**Sürüm:** 2.2 (23 Ekim 2026 — oturum ve Puantaj güncellemeleri)
**İlgili dosyalar:** [FASTREPORT_A4_STANDART.md](FASTREPORT_A4_STANDART.md) ·
[FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) ·
[CSS_DESIGN_SYSTEM.md](CSS_DESIGN_SYSTEM.md) · [TECHNICAL_DOCUMENTATION.md](TECHNICAL_DOCUMENTATION.md) ·
[PROJE_INCELEME_RAPORU.md](PROJE_INCELEME_RAPORU.md)
