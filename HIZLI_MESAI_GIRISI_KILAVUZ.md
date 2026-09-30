# Hizli Mesai Girisi Kilavuzu (Guncel)

## 21 Eylul 2026 - Modal ve Mesai Hesaplama Duzeltmeleri
- Mesai gunune tiklanip guncelleme penceresi acildiginda kayit, `GET /HizliMesaiGirisi/GetMesaiDetay?kayitId=...` ile `mesai_kayitlari` tablosundan okunur.
- MySQL `TIME` alanlari modalin `HH:mm` formatina donusturulur; bu nedenle `08:00` ve `09:30` gibi kayitli saatler input alanlarinda gorunur.
- Yeni kayit penceresi acilir acilmaz varsayilan saatlerin suresi hesaplanir. Baslangic veya bitis saati degistiginde `Süre: ...` alani aninda yenilenir.
- `08:00 - 09:30` icin gercek calisma suresi `1 saat 30 dakika`, veritabanindaki decimal karsiligi `fiili_saat = 1.50` olur.
- Hafta ici hesaplamasi: `zam01_saat = fiili_saat * 0.10`, `zam05_saat = 0`, `toplam_saat = fiili_saat + zam01_saat`.
- Hafta sonu hesaplamasi: `zam01_saat = 0`, `zam05_saat = fiili_saat * 0.50`, `toplam_saat = fiili_saat + zam05_saat`.
- Yeni kayit, guncelleme ve CSV aktarimi `fiili_saat`, `zam01_saat`, `zam05_saat` ve `toplam_saat` alanlarini birlikte yazar.
- Ay verisi, ay toplami ve admin listesi ayni saat alanlarini okur.
- Degisiklikler `Controllers/PRS/HizliMesaiGirisiController.cs` ve `Views/PRS/Mesai/HizliMesaiGirisi/Index.cshtml` dosyalarindadir.
- Dogrulama: Proje `obj\\validation` cikti klasorune basariyla derlenmistir; mevcut projeye ait uyarilar devam etmektedir.

## Kapsam
Bu dokuman, PRS altindaki Hizli Mesai Girisi modulu icin guncel davranisi aciklar.

- Sayfa: `/HizliMesaiGirisi/Index`
- Controller: `Controllers/PRS/HizliMesaiGirisiController.cs`
- View: `Views/PRS/HizliMesaiGirisi/Index.cshtml`

## Guncel Is Kurali
- Sistem `mesai_kayitlari` tablosu ile calisir.
- Kayitlar `personel_id` bazlidir.
- Popup icinde girilen `Aciklama`, veritabaninda `aciklama` kolonuna yazilir.
- Popup icindeki `Baslangic` ve `Bitis` saatleri kayitli degerlerden doldurulur.
- Popup suresi saat:dakika olarak gosterilir; veritabani saat degerleri decimal saat olarak saklar.
- Bos gun hucreleri bos gorunur (varsayilan `X` yok).
- Ayni personel + tarih icin tekrar kayit engellenir.

## Veritabani Beklentisi
Uygulamanin aktif kullandigi temel kolonlar:
- `id`
- `personel_id`
- `tarih`
- `baslangic`
- `bitis`
- `gorev`
- `fiili_saat`
- `zam01_saat`
- `zam05_saat`
- `toplam_saat`
- `aciklama`
- `kayit_tarihi`

Not: Eski dokumanlardaki `KullaniciId`, `Durum`, `Onay` odakli akis bu modulun guncel haliyla birebir uyumlu degildir.

## Endpointler
- `GET  /HizliMesaiGirisi/Index`
- `POST /HizliMesaiGirisi/KayitEkle`
- `POST /HizliMesaiGirisi/Guncelle`
- `POST /HizliMesaiGirisi/Sil`
- `GET  /HizliMesaiGirisi/GetMesaiDetay`
- `GET  /HizliMesaiGirisi/GetAyVerileri`
- `GET  /HizliMesaiGirisi/GetAyToplami`
- `POST /HizliMesaiGirisi/ImportExcel`
- `GET  /HizliMesaiGirisi/Admin`

## Son Duzeltmeler
1. `Index was outside the bounds of the array`:
   - Tarih parse islemleri guvenli hale getirildi (`DateTime.TryParse`).
2. Popup kayit akisi:
   - `Mesai Kodu` aciklamaya eklenmiyor.
   - Sadece popup `Aciklama` alani DB'ye gidiyor.
3. Yeni kayit hucre gorunumu:
   - Varsayilan `X` kaldirildi, hucre bos kalir.

## Test Adimlari
1. `dev-tools.ps1 -Action stop`
2. `dev-tools.ps1 -Action rebuild`
3. `dev-tools.ps1 -Action run -Port 5010`
4. `/HizliMesaiGirisi/Index` ac
5. Bos bir gune tikla, mesai kaydi ekle
6. Kaydin DB'de `aciklama` alanina dogru yazildigini kontrol et

## Bilinen Notlar
- Build sirasinda `NU1903` uyarilari gorunebilir (paket guvenlik advisory).
- Calisan process varken build alininca kilit hatasi alinabilir.
