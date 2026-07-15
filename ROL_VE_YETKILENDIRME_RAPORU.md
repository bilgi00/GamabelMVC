# ASP.NET Core Uygulaması - Rol ve Yetkilendirme Raporu

**Rapor Tarihi:** 11 Haziran 2026  
**Uygulama Adı:** GAMABELMVC  
**Sistem Mimarisi:** Çift Modül (PRS + STS)

---

## 📊 ÖZET

Uygulama **iki bağımsız sistem** olarak çalışmaktadır:

| Modül | Rolleri | Depo Sorumlusu | Şube Personeli | Admin |
|-------|---------|--|--|--|
| **PRS** (Personel/Mesai) | `admin`, `birim_amiri` | ❌ Yok | ❌ Yok | ✅ Evet |
| **STS** (Sevkiyat Takip) | `SubePersoneli`, `DepoSorumlusu`, `Admin` | ✅ | ✅ | ✅ |

---

## 1️⃣ PRS MODÜLÜ (Personel Raporlama Sistemi)

### 1.1 Roller Tanımı

#### 📌 **admin**
- **Veritabanı:** `admin_kullanicilar.rol = 'admin'` (eğer tanımlanmışsa, varsayılan: `birim_amiri`)
- **Veritabanı Tablosu:** `admin_kullanicilar` 
  ```sql
  CREATE TABLE admin_kullanicilar (
      id INT IDENTITY(1,1) PRIMARY KEY,
      kullanici_adi NVARCHAR(100) NOT NULL,
      sifre NVARCHAR(255) NOT NULL,
      ad NVARCHAR(100) NULL,
      soyad NVARCHAR(100) NULL,
      birim NVARCHAR(255) NULL,
      rol NVARCHAR(50) DEFAULT 'birim_amiri'
  );
  ```

#### 📌 **birim_amiri**
- **Varsayılan rol** (yeni kullanıcılar için)
- Sadece kendi bölümünün verilerine erişim

### 1.2 Yetkilendirme Kontrolü

| Kontrol | Dosya | Açıklama |
|---------|-------|---------|
| **Login** | [AccountController.cs](AccountController.cs#L38-L60) | Rol session'a yazılıyor: `HttpContext.Session.SetString("Rol", reader.IsDBNull(2) ? "birim_amiri" : reader.GetString(2));` |
| **KullaniciController.Index** | [KullaniciController.cs](Controllers/PRS/KullaniciController.cs#L20-L24) | `if (HttpContext.Session.GetString("Rol") != "admin") return RedirectToAction("Liste2");` |
| **KullaniciController.PersonelByBirim** | [KullaniciController.cs](Controllers/PRS/KullaniciController.cs#L88-L92) | Admin değilse sadece kendi birimi: `if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim)) birim = kullaniciBirim;` |

### 1.3 PRS Modülü - Kontroller ve Aksiyonlar

#### 🔑 **KullaniciController** (Admin Kullanıcı Yönetimi)

| Aksiyon | Admin | birim_amiri | İzin |
|---------|-------|---|---|
| **Index()** | ✅ | ❌ | Sadece Admin |
| **Liste2()** | ✅ | ✅ | Tüm kullanıcılar |
| **PersonelByBirim()** | ✅ Tüm birimler | ✅ Kendi birimi | Birim bazlı |

**Birim Kısıtlaması:**
```csharp
if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
{
    birim = kullaniciBirim;  // Kendi birimini zorla seç
}
```

#### 📅 **PuantajController** (Mesai İzin Yönetimi)

| Aksiyon | Admin | birim_amiri | İzin |
|---------|-------|---|---|
| **Index()** | ✅ | ✅ | Oturum gerekli |
| **GetPersoneller()** | ✅ Tüm birimler | ✅ Kendi birimi | Birim bazlı |
| **GetIzinler()** | ✅ | ✅ | Oturum gerekli |
| **KaydetIzin()** | ✅ | ✅ | POST (Toplu/Tekli) |

**SQL Sorgusu (Birim Kısıtı):**
```sql
-- Admin
SELECT id, ad, soyad, birim_adi FROM personeller ORDER BY birim_adi, ad, soyad

-- birim_amiri
SELECT id, ad, soyad, birim_adi FROM personeller 
WHERE birim_adi = @birim ORDER BY ad, soyad
```

#### 📊 **RaporController** (Personel Raporları)

| Aksiyon | Admin | birim_amiri | İzin |
|---------|-------|---|---|
| **Index()** | ✅ | ✅ | Oturum gerekli |
| **GetStatuRapor()** | ✅ Tüm birimler | ✅ Kendi birimi | Birim bazlı |

**Kısıtlama:**
```csharp
if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
    birim = kullaniciBirim;  // Kendi bölümünü zorla seç
```

#### 📆 **HizliMesaiGirisiController** (Hızlı Mesai Girişi)

| Aksiyon | Admin | birim_amiri | İzin |
|---------|-------|---|---|
| **Index()** | ✅ Tüm birimler | ✅ Kendi birimi | Birim bazlı |

#### 🏛️ **ResmiTatilController** (Resmi Tatil Yönetimi)

| Aksiyon | Admin | birim_amiri | İzin |
|---------|-------|---|---|
| **Index()** | ✅ | ✅ | Oturum gerekli |

---

## 2️⃣ STS MODÜLÜ (Sevkiyat Takip Sistemi)

---

## 3️⃣ YENİ GÜNCELLEME ÖNERİLERİ

Aşağıdaki geliştirmeler, mevcut rol yapısını daha sağlam ve yönetilebilir hale getirecektir:

### 3.1 Kullanıcı → Rol İlişkisi
- Her kullanıcıya doğrudan bir rol atanması daha temiz bir yapıdır.
- Bu sayede menü yetkileri sadece rol tablosundan değil, kullanıcı bazlı ilişkilerle de yönetilebilir.
- Önerilen yapı:
  - `roll` tablosu: rol ve yetki setlerini tutar.
  - `admin_kullanicilar` tablosuna `role_id` eklenir.
  - Girişte kullanıcıya ait rol bilgisi yüklenir.

### 3.2 İşlem Bazlı Yetki Mantığı
- Sadece menü görünürlüğü değil, aynı zamanda işlem seviyesinde erişim de kontrol edilmelidir.
- Önerilen yetki türleri:
  - `ekleme`
  - `duzenleme`
  - `silme`
  - `onaylama`
  - `rapor_goruntuleme`

### 3.3 URL Erişim Kontrolü
- Menüde görünmeyen sayfalara doğrudan erişimi engellemek gerekir.
- Bu, güvenlik ve kullanıcı deneyimi açısından önemlidir.
- Her controller aksiyonunda yetki kontrolü uygulanmalıdır.

### 3.4 Admin Paneli Geliştirmesi
- Admin panelinden şu işlemler yapılabilmelidir:
  - Yeni rol ekleme
  - Mevcut rolü düzenleme
  - Kullanıcıya rol atama
  - Rol bazlı menü yetkilerini değiştirme

### 3.5 Uygulama İçin Önerilen Öncelik Sırası
1. Kullanıcı → rol ilişkisini kurmak
2. Menüler için URL erişim kontrolünü eklemek
3. İşlem bazlı yetki alanlarını tanımlamak
4. Admin panelinden rol ve kullanıcı atama işlevlerini aktif hale getirmek

Bu yaklaşım ile sistem hem daha modüler hem de daha sürdürülebilir hale gelecektir.

### 2.1 Roller Tanımı

**Veritabanı Tablosu:**
```sql
CREATE TABLE stk_Kullanici (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SubeId INT NOT NULL,
    AdSoyad VARCHAR(100) NOT NULL,
    KullaniciAdi VARCHAR(50) NOT NULL UNIQUE,
    SifreHash VARCHAR(255) NOT NULL,
    Rol ENUM('SubePersoneli', 'DepoSorumlusu', 'Admin') NOT NULL DEFAULT 'SubePersoneli',
    SonGirisTarihi DATETIME NULL,
    AktifMi BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (SubeId) REFERENCES stk_Sube(Id),
    INDEX idx_rol (Rol)
);
```

#### 📌 **SubePersoneli** (Şube Personeli)
- **Veritabanında:** `stk_Kullanici.Rol = 'SubePersoneli'`
- **Varsayılan Rol:** Yeni STS kullanıcıları için
- **Erişimi:** Login yapabiliyor ama kısıtlı aksiyonlar
- **Kısıtlamalar:**
  - Eksik Kayıt: Sadece kendi şubesinin eksiklerini görebiliyor
  - Sevkiyat, Rapor, Sipariş: **Erişim Yok**

**Erişim Kontrol Örneği:**
```csharp
// [SevkiyatController.Index]
var rol = HttpContext.Session.GetString("Rol") ?? "";
if (rol != "DepoSorumlusu" && rol != "Admin")
    return RedirectToAction("Index", "Home");  // Reddedildi
```

#### 📌 **DepoSorumlusu** (Depo Sorumlusu)
- **Veritabanında:** `stk_Kullanici.Rol = 'DepoSorumlusu'`
- **Şube:** Ana Depo (`stk_Sube.Kod = 'ANADEPO'`)
- **Test Kullanıcısı:** `depo1` / `123456` (BCrypt)
  ```sql
  INSERT INTO stk_Kullanici (SubeId, AdSoyad, KullaniciAdi, SifreHash, Rol, AktifMi)
  VALUES (@SubeId, 'Test Depo Sorumlusu', 'depo1', '$2a$11$UqcRGJQh7e6w8N6y5X3Q4uLzKdFD3K7F8Q0Z3R2C1P9O8N7M6L5K', 'DepoSorumlusu', 1);
  ```
- **Erişimi:** Tüm merkezi operasyonlara erişim

#### 📌 **Admin** (Sistem Yöneticisi)
- **Veritabanında:** `stk_Kullanici.Rol = 'Admin'`
- **Erişimi:** Tüm sistemin kontrolü + yedekleme/geri yükleme

### 2.2 Yetkilendirme Kontrolü

| Kontrol | Dosya | Açıklama |
|---------|-------|---------|
| **STS Login** | [AccountController.cs](AccountController.cs#L94-L150) | Rol session'a yazılıyor: `HttpContext.Session.SetString("Rol", reader["Rol"]?.ToString() ?? "SubePersoneli");` |
| **Şifre Hash Kontrolü** | [AccountController.cs](AccountController.cs#L136-L143) | BCrypt + düz metin geçiş: `if (sifreHash.StartsWith("$2")) { sifreDogru = BCrypt.Net.BCrypt.Verify(...); }` |

### 2.3 STS Modülü - Kontroller ve Aksiyonlar

#### 🚚 **SevkiyatController** (Sevkiyat Yönetimi)

| Aksiyon | Admin | DepoSorumlusu | SubePersoneli | İzin |
|---------|-------|--|--|---|
| **Index()** | ✅ | ✅ | ❌ | Sadece DepoSorumlusu/Admin |
| Sevk Ekle | ✅ | ✅ | ❌ | Sadece DepoSorumlusu/Admin |
| Sevk Onayla | ✅ | ✅ | ❌ | Sadece DepoSorumlusu/Admin |

**Kod Örneği:**
```csharp
public async Task<IActionResult> Index(string? subeAdi = null, string? firma = null)
{
    var rol = HttpContext.Session.GetString("Rol") ?? "";
    if (rol != "DepoSorumlusu" && rol != "Admin")
        return RedirectToAction("Index", "Home");
    // ...
}
```

**Filtre Özelliği:**
- Session'da filtreler saklanır: `LastSubeFilter`, `LastFirmaFilter`, `LastGrupFilter`
- Sayfada Bölüm, Firma, Grup filtreleme
- Onay bekleyen eksikler ayrı liste

#### ❌ **EksikController** (Eksik Kayıt Yönetimi)

| Aksiyon | Admin | DepoSorumlusu | SubePersoneli | İzin |
|---------|-------|--|--|---|
| **SubeListe()** | ✅ | ✅ | ✅ | Oturum gerekli |
| Kendi eksikleri | ✅ | ✅ | ✅ | Şube bazlı |
| **Toplu Ekleme** | ✅ | ✅ | ✅ | AJAX POST |
| **UrunAra()** | ✅ | ✅ | ✅ | Autocomplete |
| **BarkodAra()** | ✅ | ✅ | ✅ | Barcode scan |

**Eksik Girileme Kontrol:**
- Pazartesi 00:00 ile Cuma 23:59 arasında girileme açık
- Hafta kapanması kontrolü (`stk_HaftaKapanis`)

#### 📋 **SiparisController** (Fabrika Siparişleri)

| Aksiyon | Admin | DepoSorumlusu | SubePersoneli | İzin |
|---------|-------|--|--|---|
| **FabrikaListe()** | ✅ | ✅ | ❌ | Sadece DepoSorumlusu/Admin |
| Firma Filtreleme | ✅ | ✅ | ❌ | - |
| PDF İhracat | ✅ | ✅ | ❌ | - |

#### 📊 **RaporStsController** (STS Raporları)

| Aksiyon | Admin | DepoSorumlusu | SubePersoneli | İzin |
|-------|-------|--|--|---|
| **Index()** | ✅ | ✅ | ✅ | Oturum gerekli (Açık!) |
| En çok eksik ürün | ✅ | ✅ | ✅ | - |
| Geç giren şubeler | ✅ | ✅ | ✅ | - |
| Acil eksikler | ✅ | ✅ | ✅ | - |
| **GeçmisHareketler()** | ✅ | ✅ | ✅ | Açık (Tüm işlem logu) |
| **DetailedRapor()** | ✅ | ✅ | ✅ | Açık |

**⚠️ AÇIKLIK:** RaporStsController'da rol kontrolü **YOKTUR**. Herhangi bir STS kullanıcısı raporlara erişebilir.

#### 📈 **RaporController** (PRS Raporları - Ayrı sistem)

Bu kontroller PRS modülünde olup, STS login yapan kullanıcılar erişemez.

#### 🛟 **AdminController** (Sistem Yönetimi)

| Aksiyon | Admin | DepoSorumlusu | SubePersoneli | İzin |
|---------|-------|--|--|---|
| **IsAdmin()** | ✅ | ❌ | ❌ | `if (rol == "Admin") return true;` |
| **VeritabaniYedekleme()** | ✅ | ❌ | ❌ | Sadece Admin |
| **YedekleSeciliTablolar()** | ✅ | ❌ | ❌ | Sadece Admin |
| **GeriYukleSeciliTablolar()** | ✅ | ❌ | ❌ | Sadece Admin |

**Yedekleme Kontrol:**
```csharp
private bool IsAdmin()
{
    var rol = HttpContext.Session.GetString("Rol") ?? "";
    return rol == "Admin";
}
```

---

## 3️⃣ ŞUBEPERSONELİ vs DEPO SORUMLUSU FARKLARI

### 📍 Erişim Özeti

| Özellik | SubePersoneli | DepoSorumlusu | Admin |
|---------|--|--|--|
| **Login Yapma** | ✅ | ✅ | ✅ |
| **Kendi Eksik Kayıtları Görme** | ✅ | ✅ | ✅ |
| **Eksik Girleri Tutma** | ✅ | ✅ | ✅ |
| **Sevkiyat Listesi** | ❌ | ✅ | ✅ |
| **Sevkiyat Onaylama** | ❌ | ✅ | ✅ |
| **Fabrika Siparişleri** | ❌ | ✅ | ✅ |
| **Raporlar (STS)** | ✅ | ✅ | ✅ |
| **Veritabanı Yedekleme** | ❌ | ❌ | ✅ |
| **Sistem Yönetimi** | ❌ | ❌ | ✅ |

### 🔍 Teknik Farklılıklar

#### **1. Şube Kısıtlaması**

**SubePersoneli:**
- `stk_Kullanici.SubeId` ile bağlı spesifik şubeye
- Sadece kendi şubesinin eksiklerini görebiliyor
- `SubeListe()` aksiyonunda: `WHERE SubeId = @subeId`

**DepoSorumlusu:**
- Ana Depo'ya bağlı (`stk_Sube.Kod = 'ANADEPO'`)
- Tüm şubelerin verilerine erişim
- Filtreler aracılığıyla bölüm seçebiliyor

#### **2. Kontrol Yazılımı**

```csharp
// SubePersoneli erişimi
var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
var eksikler = db.Query($"... WHERE SubeId = {subeId}");

// DepoSorumlusu/Admin erişimi
var rol = HttpContext.Session.GetString("Rol");
if (rol != "DepoSorumlusu" && rol != "Admin")
    return RedirectToAction("Index", "Home");
```

---

## 4️⃣ VERİTABANI YAPILARI

### 4.1 STS Modülü Tabloları

```sql
-- Kullanıcılar (Rol tanımı)
stk_Kullanici (Rol: SubePersoneli | DepoSorumlusu | Admin)

-- Şubeler (depo + şubeler)
stk_Sube (Tip: Sube | AnaDepo)

-- Ürünler
stk_Urun (Firma, Grup, Birim)

-- Eksik Kayıtları
stk_EksikKaydi (Durum: Bekliyor | SevkEdildi | Tamamlandi)

-- Sevkiyatlar
stk_Sevkiyat (Durum: Hazirlaniyor | Yolda | TeslimEdildi | OnayBekliyor | Onaylandi)

-- Fabrika Siparişleri
stk_FabrikaSiparisi (Durum: SiparisVerildi | Uretimde | Yolda | TeslimAlindi)

-- İşlem Logu (Denetim)
stk_HareketLog (IslemTipi: Ekle | Guncelle | Sil | Duzeltme)

-- Hafta Kapanış
stk_HaftaKapanis (Geç giren şubeler takibi)
```

### 4.2 PRS Modülü Tabloları

```sql
-- Admin Kullanıcıları (Rol: admin | birim_amiri)
admin_kullanicilar

-- Personel Bilgileri
personeller (per_statu: Görev adı/statüsü)

-- Bölüm/Birim
birimler

-- Puantaj İzinleri
puantaj_izin (izin_tipi, tarih)

-- Mesai Kayıtları
mesai_kayitlari

-- Resmi Tatiller
kktc_resmi_tatiller
```

---

## 5️⃣ ROL KONTROLÜ MEKANIZMLARI

### 5.1 Session Bazlı Kontrol

Tüm yetkilendirme **HTTP Session** üzerinden yapılıyor:

```csharp
// Login sırasında yazılıyor
HttpContext.Session.SetString("Rol", rolDegeri);
HttpContext.Session.SetInt32("SubeId", subeId);
HttpContext.Session.SetInt32("KullaniciId", kullaniciId);

// Aksiyonda kontrol ediliyor
var rol = HttpContext.Session.GetString("Rol");
if (rol != "DepoSorumlusu" && rol != "Admin")
    return RedirectToAction("Index", "Home");
```

### 5.2 Yetkilendirme Desenleri

**Pattern 1: Tam Engelleme**
```csharp
if (rol != "DepoSorumlusu" && rol != "Admin")
    return RedirectToAction("Index", "Home");
```

**Pattern 2: Birim Kısıtlaması**
```csharp
if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
    birim = kullaniciBirim;  // Kendi birimine zorla ayarla
```

**Pattern 3: Açık Erişim (Uyarı⚠️)**
```csharp
// RaporStsController.Index() - Rol kontrolü YOK!
public async Task<IActionResult> Index()
{
    // Herhangi bir STS kullanıcısı giriş yapabilir
}
```

---

## 6️⃣ GÜVENLİK BULGUSU VE ÖNERILER

### ✅ İyi Uygulamalar

1. **Session Tabanlı Yönetim:** Rol bilgisi güvenli şekilde oturum verisinde tuttuluyor
2. **Veritabanında ENUM Tipi:** Rol değerleri enum sınırlandırması ile
3. **Şifre Hash:** BCrypt kullanılıyor (`$2a$11$...`)
4. **Geçiş Dönemi:** Eski düz metin şifreler de destekleniyor (uyumlu)
5. **Denetim Günlüğü:** `stk_HareketLog` tablosunda tüm işlemler kaydediliyor

### ⚠️ Potansiyel Riskler

1. **RaporStsController'da Eksik Kontrol**
   - `Index()`, `GeçmisHareketler()`, `DetailedRapor()` aksiyonlarında rol kontrolü **YOK**
   - **Risk:** SubePersoneli de STS raporlarına erişebiliyor
   - **Öneri:** DepoSorumlusu/Admin kontrolü ekle

   ```csharp
   public async Task<IActionResult> Index()
   {
       var rol = HttpContext.Session.GetString("Rol") ?? "";
       if (rol != "DepoSorumlusu" && rol != "Admin")
           return RedirectToAction("Index", "Home");
       // ...
   }
   ```

2. **İşlem Logu Açık Erişim**
   - GeçmisHareketler() tüm işlem geçmişini gösteriyor
   - **Risk:** SubePersoneli diğer şubelerin işlemlerini görebiliyor
   - **Öneri:** Şube bazlı filtre ekle

3. **Birim Bazlı Kontrol Tutarsızlığı**
   - PRS modülünde `birim` parametresi zorlanıyor
   - STS modülünde `SubeId` kontrolü eksik (filtreleme seviyesinde)
   - **Öneri:** Şube bazlı filtreler eklenerek doğrulanması

4. **Admin Password Policy**
   - Test kullanıcıları sabit şifreli veritabanında
   - **Öneri:** Şifre değiştirme zorunlu tutulmalı

---

## 7️⃣ DOSYA HARİTASI

### PRS Modülü Dosyaları
- [Controllers/AccountController.cs](Controllers/AccountController.cs) - Login/Logout
- [Controllers/PRS/KullaniciController.cs](Controllers/PRS/KullaniciController.cs) - Kullanıcı Yönetimi
- [Controllers/PRS/PuantajController.cs](Controllers/PRS/PuantajController.cs) - İzin Yönetimi
- [Controllers/PRS/RaporController.cs](Controllers/PRS/RaporController.cs) - Raporlar
- [Controllers/PRS/MesaiController.cs](Controllers/PRS/MesaiController.cs) - Mesai İzleme
- [Controllers/PRS/HizliMesaiGirisiController.cs](Controllers/PRS/HizliMesaiGirisiController.cs) - Hızlı Giriş
- [Controllers/PRS/ResmiTatilController.cs](Controllers/PRS/ResmiTatilController.cs) - Resmi Tatiller

### STS Modülü Dosyaları
- [Controllers/AccountController.cs](Controllers/AccountController.cs) - STS Login
- [Controllers/STS/SevkiyatController.cs](Controllers/STS/SevkiyatController.cs) - Sevkiyat Yönetimi
- [Controllers/STS/EksikController.cs](Controllers/STS/EksikController.cs) - Eksik Kayıtları
- [Controllers/STS/SiparisController.cs](Controllers/STS/SiparisController.cs) - Fabrika Siparişleri
- [Controllers/STS/RaporStsController.cs](Controllers/STS/RaporStsController.cs) - ⚠️ Rol Kontrolü Eksik
- [Controllers/AdminController.cs](Controllers/AdminController.cs) - Sistem Yönetimi

### Veritabanı Dosyaları
- [sql/sqltasarim_sts.sql](sql/sqltasarim_sts.sql) - STS Şema
- [sql/sqltasarim.sql](sql/sqltasarim.sql) - PRS Şema
- [sql/insert_test_user.sql](sql/insert_test_user.sql) - Test Verileri

### Dokümantasyon
- [FILTER_FEATURE_DOCUMENTATION.md](FILTER_FEATURE_DOCUMENTATION.md) - Filtre Özelliği
- [Program.cs](Program.cs) - Uygulama Başlatma

---

## 📋 SONUÇ TABLOSU

### Rol Özeti Matrisi

```
┌──────────────────┬─────────┬──────────────┬─────────────────┐
│   FÖZELLİK       │  Admin  │ DepoSorumluu │  SubePersoneli  │
├──────────────────┼─────────┼──────────────┼─────────────────┤
│ PRS Erişim       │    ❌   │      ❌      │       ❌        │
│ STS Erişim       │    ✅   │      ✅      │       ✅        │
│ Sevkiyat Yönet.  │    ✅   │      ✅      │       ❌        │
│ Eksik Girişi     │    ✅   │      ✅      │       ✅        │
│ Raporlar         │    ✅   │      ✅      │       ✅ (Risk) │
│ Sistem Yönet.    │    ✅   │      ❌      │       ❌        │
│ Veritabanı Yede  │    ✅   │      ❌      │       ❌        │
└──────────────────┴─────────┴──────────────┴─────────────────┘
```

---

**Hazırlayan:** Kod Analiz Sistemi  
**Sonraki Adım:** Rol kontrolü eksiklikleri düzeltme  
**Öncelik:** RaporStsController yetkilendirme kontrolü ekleme
