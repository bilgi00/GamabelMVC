# Hizli Mesai Girisi Kilavuzu (Guncel)

## Kapsam
Bu dokuman, PRS altindaki Hizli Mesai Girisi modulu icin guncel davranisi aciklar.

- Sayfa: `/HizliMesaiGirisi/Index`
- Controller: `Controllers/PRS/HizliMesaiGirisiController.cs`
- View: `Views/PRS/HizliMesaiGirisi/Index.cshtml`

## Guncel Is Kurali
- Sistem `mesai_kayitlari` tablosu ile calisir.
- Kayitlar `personel_id` bazlidir.
- Popup icinde girilen `Aciklama`, veritabaninda `aciklama` kolonuna yazilir.
- Bos gun hucreleri bos gorunur (varsayilan `X` yok).
- Ayni personel + tarih icin tekrar kayit engellenir.

## Veritabani Beklentisi
Uygulamanin aktif kullandigi temel kolonlar:
- `id`
- `personel_id`
- `tarih`
- `baslangic`
- `bitis`
- `toplam_saat`
- `aciklama`

Not: Eski dokumanlardaki `KullaniciId`, `Durum`, `Onay` odakli akis bu modulun guncel haliyla birebir uyumlu degildir.

## Endpointler
- `GET  /HizliMesaiGirisi/Index`
- `POST /HizliMesaiGirisi/KayitEkle`
- `POST /HizliMesaiGirisi/Guncelle`
- `POST /HizliMesaiGirisi/Sil`
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
