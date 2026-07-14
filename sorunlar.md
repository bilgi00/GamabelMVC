# Sorunlar Backlog

Tarih: 2026-06-16
Kaynak: dotnet build (Debug, net9.0)
Durum: Daha sonra duzeltilecek acik maddeler

## Ozet
- Build: Basarili (exit code 0)
- Toplam uyari: 46
- Kritik hata: Yok
- Onceliklendirme: Guvenlik > Nullability > Async temizligi

## 1) Guvenlik Uyarisi (Yuksek Oncelik)

- [ ] NU1903: System.Formats.Asn1 8.0.0 paketinde yuksek siddette bilinen acik var
  - Dosya: gamabelmvc.csproj
  - Danismanlik: GHSA-447r-wph3-92pm
  - Plan: Bagimli paketi guvenli surume yukselterek tekrar build almak

## 2) Nullability Uyarilari (Orta Oncelik)

### 2.1 Model null atanamaz ozellikler (CS8618)
- [ ] Models/PRS/SikayetModel.cs:10
- [ ] Models/PRS/SikayetModel.cs:11
- [ ] Models/PRS/SikayetModel.cs:12
- [ ] Models/PRS/SikayetModel.cs:13
- [ ] Models/PRS/SikayetModel.cs:15
- [ ] Controllers/STS/EksikController.cs:15

Plan:
- Gerekli alanlara `required` ekle veya nullable (`string?`) yap.
- DTO/Model olusturma akislarinda varsayilan atamalarla garanti sagla.

### 2.2 Olasi null atama/kullanim (CS8600/CS8601/CS8602/CS8605)
- [ ] Controllers/PRS/SikayetController.cs:39
- [ ] Controllers/PRS/SikayetController.cs:40
- [ ] Controllers/PRS/SikayetController.cs:95
- [ ] Controllers/PRS/SikayetController.cs:96
- [ ] Controllers/STS/EksikController.cs:260
- [ ] Controllers/STS/SevkiyatController.cs:996
- [ ] Controllers/STS/SevkiyatController.cs:1027
- [ ] Controllers/STS/SevkiyatController.cs:1061
- [ ] Views/STS/Admin/Kullanicilar.cshtml:25
- [ ] Views/STS/Admin/Kullanicilar.cshtml:40
- [ ] Views/STS/Admin/Urunler.cshtml:26
- [ ] Views/STS/Admin/Urunler.cshtml:56
- [ ] Views/STS/Eksik/SubeListe.cshtml:39
- [ ] Views/STS/Eksik/SubeListe.cshtml:57
- [ ] Views/STS/Eksik/Gecmis.cshtml:16
- [ ] Views/STS/Eksik/Gecmis.cshtml:33
- [ ] Views/STS/RaporSts/GeçmisHareketler.cshtml:13
- [ ] Views/STS/RaporSts/GeçmisHareketler.cshtml:29
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:107
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:159
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:160
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:161
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:162
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:163
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:164
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:166
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:167
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:168
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:169
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:170
- [ ] Views/STS/RaporSts/DetailedRapor.cshtml:303
- [ ] Views/STS/Siparis/FabrikaListe.cshtml:57
- [ ] Views/STS/Siparis/FabrikaListe.cshtml:75
- [ ] Views/STS/Sevkiyat/SubeOnay.cshtml:51
- [ ] Views/STS/Sevkiyat/SubeOnay.cshtml:81

Plan:
- Null-kontrolu (`?.`, `??`, `is not null`) ekle.
- Razor tarafinda model/collection null guard kullan.
- Dynamic/readerlarda donusum oncesi `DBNull` ve null kontrolu zorunlu hale getir.

## 3) Async Uyarilari (Dusuk-Orta Oncelik)

- [ ] CS1998: Controllers/PRS/HizliMesaiGirisiController.cs:439
- [ ] CS1998: Controllers/STS/EksikController.cs:375
- [ ] CS1998: Controllers/PRS/KullaniciController.cs:176

Plan:
- Gercek asenkron islem yoksa metodu senkron hale getir.
- Varsa ilgili I/O cagrisini `await` ile asenkron kullan.

## Cozum Sirasi Onerisi
1. NU1903 guvenlik acigini kapat
2. CS8618 model uyarilarini temizle
3. Controller nullability (CS860x) uyarilarini azalt
4. Razor nullability (CS8602/CS8605) uyarilarini temizle
5. CS1998 async duzenlemesi

## Not
- Bu dosya, bugun tespit edilip ertelenen teknik borclari takip etmek icin olusturuldu.
- Her tamamlanan madde kapatildiginda kutucuk isaretlenecek ve tarih notu dusulecek.
