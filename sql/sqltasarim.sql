
IF OBJECT_ID('stk_Sikayet', 'U') IS NULL
CREATE TABLE stk_Sikayet (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Tarih DATETIME NOT NULL DEFAULT GETDATE(),
    KullaniciId INT NOT NULL,
    KullaniciAdi NVARCHAR(100) NOT NULL,
    Konu NVARCHAR(255) NOT NULL,
    Detay NVARCHAR(MAX),
    Sonuc NVARCHAR(MAX),
    SonucTarihi DATETIME NULL,
    Durum NVARCHAR(20) NOT NULL DEFAULT 'Açık'
);

-- Bildirim sistemi için öneri:
-- Yeni şikayet eklendiğinde admin'e, sonuç girildiğinde ilgili kullanıcıya bildirim gönderilebilir.
-- Bildirimler için ayrı bir tablo eklenebilir:
-- CREATE TABLE IF NOT EXISTS Bildirim (
--     Id INT AUTO_INCREMENT PRIMARY KEY,
--     KullaniciId INT NOT NULL,
--     Mesaj TEXT NOT NULL,
--     OkunduMu TINYINT(1) DEFAULT 0,
--     Tarih DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
-- =============================================
-- GAMABELMVC - MySQL Veritabanı Tasarımı
-- =============================================
-- Bu dosya sadece bilgi amaçlıdır.
-- Uygulama içerisinde hiçbir yere bağlantısı yoktur.
-- Tüm tablo yapıları ve kullanılan sorgular burada belgelenmiştir.
-- =============================================

-- =============================================
-- BAĞLANTI BİLGİLERİ
-- =============================================
-- Server   : 94.73.145.55
-- Database : u2636310_dbE97
-- User     : u2636310_userE97
-- Kaynak   : appsettings.json -> ConnectionStrings.MyConnection



IF OBJECT_ID('admin_kullanicilar', 'U') IS NULL
CREATE TABLE admin_kullanicilar (
    id INT IDENTITY(1,1) PRIMARY KEY,
    kullanici_adi NVARCHAR(100) NOT NULL,
    sifre NVARCHAR(255) NOT NULL,
    ad NVARCHAR(100) NULL,
    soyad NVARCHAR(100) NULL,
    telefon NVARCHAR(20) NULL,
    birim NVARCHAR(255) NULL,
    rol NVARCHAR(50) DEFAULT 'birim_amiri'
);



IF OBJECT_ID('birimler', 'U') IS NULL
CREATE TABLE birimler (
    id INT IDENTITY(1,1) PRIMARY KEY,
    birim_adi NVARCHAR(255) NOT NULL
);



IF OBJECT_ID('personeller', 'U') IS NULL
CREATE TABLE personeller (
    id INT IDENTITY(1,1) PRIMARY KEY,
    per_no NVARCHAR(50) NULL,
    ad NVARCHAR(100) NULL,
    soyad NVARCHAR(100) NULL,
    birim_adi NVARCHAR(255) NULL,
    per_statu NVARCHAR(100) NULL,
    CONSTRAINT uq_per_no UNIQUE (per_no)
);

-- Mevcut tabloya per_no eklemek için:
-- ALTER TABLE personeller ADD COLUMN per_no VARCHAR(50) DEFAULT NULL AFTER id;
-- ALTER TABLE personeller ADD UNIQUE KEY uq_per_no (per_no);


-- =============================================
-- KULLANILAN SORGULAR (Uygulama seviyesi - @param .NET parametresidir)
-- =============================================

-- [AccountController - Login]
-- Kullanıcı giriş doğrulama + birim ve rol bilgisi
-- SELECT birim, rol FROM admin_kullanicilar
-- WHERE kullanici_adi = @kullaniciAdi AND sifre = @sifre;

-- [AccountController - Register]
-- Kullanıcı adı mükerrer kontrolü
-- SELECT COUNT(*) FROM admin_kullanicilar
-- WHERE kullanici_adi = @kullaniciAdi;

-- [AccountController - Register]
-- Yeni kullanıcı kaydı ekleme (birim dahil)
-- INSERT INTO admin_kullanicilar (kullanici_adi, sifre, birim)
-- VALUES (@kullaniciAdi, @sifre, @birim);

-- [AccountController - Register / LoadBirimler]
-- Kayıt sayfasında birim açılır menü
-- SELECT birim_adi FROM birimler ORDER BY birim_adi;

-- [KullaniciController - Index]
-- Tüm kullanıcıları listeleme (sadece admin)
-- SELECT * FROM admin_kullanicilar;

-- [KullaniciController - Liste2]
-- Birim listesini çekme (açılır menü için)
-- SELECT birim_adi FROM birimler ORDER BY birim_adi;

-- [KullaniciController - PersonelByBirim]
-- Seçilen birime göre personel listeleme (statü dahil)
-- (personeller.birim_adi alanı birim adıyla eşleşir)
-- SELECT ad, soyad, per_statu FROM personeller
-- WHERE birim_adi = @birim ORDER BY ad, soyad;



IF OBJECT_ID('puantaj_izin', 'U') IS NULL
CREATE TABLE puantaj_izin (
    id INT IDENTITY(1,1) PRIMARY KEY,
    personel_id INT NOT NULL,
    yil INT NOT NULL,
    ay INT NOT NULL,
    gun INT NOT NULL,
    izin_tipi NVARCHAR(10) NOT NULL,
    aciklama NVARCHAR(500) NULL,
    kayit_tarihi DATETIME DEFAULT GETDATE(),
    CONSTRAINT uq_personel_gun UNIQUE (personel_id, yil, ay, gun),
    INDEX idx_yil_ay (yil, ay),
    INDEX idx_personel (personel_id),
    CONSTRAINT fk_puantaj_personel FOREIGN KEY (personel_id) REFERENCES personeller(id) ON DELETE CASCADE
);


-- =============================================
-- PUANTAJ SAYFA SORGULARI (Uygulama seviyesi)
-- =============================================

-- [PuantajController - GetPersoneller]
-- Tüm personelleri getir
-- SELECT id, ad, soyad, birim_adi FROM personeller ORDER BY birim_adi, ad, soyad;

-- [PuantajController - GetPersoneller] (birim filtreli)
-- SELECT id, ad, soyad, birim_adi FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad;

-- [PuantajController - GetIzinler]
-- Belirli ay/yıl için izin kayıtlarını getir
-- SELECT id, personel_id, gun, izin_tipi, aciklama FROM puantaj_izin
-- WHERE yil = @yil AND ay = @ay;

-- [PuantajController - KaydetIzin]
-- İzin kaydı ekle veya güncelle (UPSERT)
-- Eğer aynı personel+yıl+ay+gün varsa güncelle, yoksa ekle
-- INSERT INTO puantaj_izin (personel_id, yil, ay, gun, izin_tipi, aciklama)
-- VALUES (@pid, @yil, @ay, @gun, @tip, @aciklama)
-- ON DUPLICATE KEY UPDATE izin_tipi = @tip, aciklama = @aciklama;

-- [PuantajController - KaydetIzin]
-- Çalışılan güne dönüştürüldüğünde izin kaydını sil
-- (X = çalıştı demek, DB'de tutulmaz)
-- DELETE FROM puantaj_izin
-- WHERE personel_id = @pid AND yil = @yil AND ay = @ay AND gun = @gun;


-- =============================================
-- RAPOR SAYFA SORGULARI (Uygulama seviyesi)
-- =============================================

-- [RaporController - GetStatuRapor]
-- Seçilen birime göre personel statü dağılımı
-- SELECT IFNULL(per_statu, 'Belirtilmemiş') AS statu, COUNT(*) AS toplam
-- FROM personeller WHERE birim_adi = @birim
-- GROUP BY per_statu ORDER BY toplam DESC;

-- [RaporController - GetStatuRapor] (Tüm birimler - sadece admin)
-- birim = '__TUMU__' olduğunda tüm personeller
-- SELECT IFNULL(per_statu, 'Belirtilmemiş') AS statu, COUNT(*) AS toplam
-- FROM personeller GROUP BY per_statu ORDER BY toplam DESC;


-- =============================================
-- EXCEL AKTARIM SORGULARI (Uygulama seviyesi)
-- =============================================

-- [KullaniciController - ExcelAktar]
-- Sütun eşleştirme: KODU=per_no, ADI=ad, SOYADI=soyad, BİRİM ADI=birim_adi, MESLEĞİ GÖREVİ=per_statu

-- per_no mevcutsa ad kontrolü (aynı kişi mi?)
-- SELECT ad FROM personeller WHERE per_no = @per_no;

-- per_no varsa UPSERT (ad farklıysa güncelle, yoksa ekle)
-- INSERT INTO personeller (per_no, ad, soyad, birim_adi, per_statu)
-- VALUES (@per_no, @ad, @soyad, @birim, @statu)
-- ON DUPLICATE KEY UPDATE ad=@ad, soyad=@soyad, birim_adi=@birim, per_statu=@statu;

-- per_no yoksa doğrudan INSERT
-- INSERT INTO personeller (ad, soyad, birim_adi, per_statu)
-- VALUES (@ad, @soyad, @birim, @statu);



IF OBJECT_ID('mesai_kayitlari', 'U') IS NULL
CREATE TABLE mesai_kayitlari (
    id INT IDENTITY(1,1) PRIMARY KEY,
    personel_id INT NOT NULL,
    tarih DATE NOT NULL,
    gorev NVARCHAR(100) NULL,
    baslangic TIME NOT NULL,
    bitis TIME NOT NULL,
    fiili_saat DECIMAL(5,2) DEFAULT 0,
    zam01_saat DECIMAL(5,2) DEFAULT 0,
    zam05_saat DECIMAL(5,2) DEFAULT 0,
    toplam_saat DECIMAL(5,2) DEFAULT 0,
    aciklama NVARCHAR(MAX) NULL,
    kayit_tarihi DATETIME DEFAULT GETDATE(),
    INDEX idx_personel (personel_id),
    INDEX idx_tarih (tarih),
    FOREIGN KEY (personel_id) REFERENCES personeller(id) ON DELETE CASCADE
);



IF OBJECT_ID('mesaisaat', 'U') IS NULL
CREATE TABLE mesaisaat (
    id INT IDENTITY(1,1) PRIMARY KEY,
    saatlik_brut DECIMAL(10,2) NOT NULL DEFAULT 0,
    gecerlilik_tarihi DATE NULL
);

-- Varsayılan değer ekle (güncellenmesi gerekir)
-- INSERT INTO mesaisaat (saatlik_brut) VALUES (100.00);


-- =============================================
-- MESAİ SAYFA SORGULARI (Uygulama seviyesi)
-- =============================================

-- [MesaiController - GetPersoneller]
-- Birime göre personel listesi (görev = per_statu)
-- SELECT id, ad, soyad, per_statu FROM personeller
-- WHERE birim_adi = @birim ORDER BY ad, soyad;

-- [MesaiController - GetMesaiKayitlari]
-- Belirli ay/yıl ve birime göre mesai kayıtlarını getir
-- SELECT mk.id, mk.personel_id, CONCAT(p.ad, ' ', p.soyad) AS ad_soyad,
--        mk.tarih, mk.gorev, mk.baslangic, mk.bitis,
--        mk.fiili_saat, mk.zam01_saat, mk.zam05_saat, mk.toplam_saat, mk.aciklama
-- FROM mesai_kayitlari mk
-- INNER JOIN personeller p ON p.id = mk.personel_id
-- WHERE p.birim_adi = @birim AND YEAR(mk.tarih) = @yil AND MONTH(mk.tarih) = @ay
-- ORDER BY mk.tarih, p.ad, p.soyad;

-- [MesaiController - KaydetMesai] (Yeni kayıt)
-- INSERT INTO mesai_kayitlari (personel_id, tarih, gorev, baslangic, bitis, fiili_saat, zam01_saat, zam05_saat, toplam_saat, aciklama)
-- VALUES (@pid, @tarih, @gorev, @bas, @bit, @fiili, @zam01, @zam05, @toplam, @aciklama);
-- SELECT LAST_INSERT_ID();

-- [MesaiController - KaydetMesai] (Güncelleme)
-- UPDATE mesai_kayitlari SET personel_id=@pid, tarih=@tarih, gorev=@gorev,
--        baslangic=@bas, bitis=@bit, fiili_saat=@fiili, zam01_saat=@zam01,
--        zam05_saat=@zam05, toplam_saat=@toplam, aciklama=@aciklama WHERE id=@id;

-- [MesaiController - SilMesai]
-- DELETE FROM mesai_kayitlari WHERE id = @id;


-- =============================================
-- TABLO İLİŞKİLERİ (RELATIONSHIP MAP)
-- =============================================
-- 
-- birimler.birim_adi
--   ├── admin_kullanicilar.birim  (mantıksal bağ, string eşleşme)
--   └── personeller.birim_adi    (mantıksal bağ, string eşleşme)
--
-- personeller.id
--   ├── puantaj_izin.personel_id  (FOREIGN KEY, ON DELETE CASCADE)
--   └── mesai_kayitlari.personel_id (FOREIGN KEY, ON DELETE CASCADE)
--
-- NOT: birimler tablosu ile admin_kullanicilar ve personeller arasında
--      resmi FOREIGN KEY yoktur. Birim eşleşmesi birim_adi string
--      alanı üzerinden yapılmaktadır.
-- NOT: Bir personel silindiğinde, ona ait tüm puantaj ve mesai
--      kayıtları da CASCADE ile otomatik silinir.



IF OBJECT_ID('kktc_resmi_tatiller', 'U') IS NULL
CREATE TABLE kktc_resmi_tatiller (
    id INT IDENTITY(1,1) PRIMARY KEY,
    tatil_adi NVARCHAR(100) NOT NULL,
    tatil_tarihi DATE NOT NULL,
    tatil_turu NVARCHAR(50) NOT NULL,
    gun_adı NVARCHAR(20) NOT NULL,
    kacinci_gun NVARCHAR(10),
    aciklama NVARCHAR(MAX)
);

-- 2026 Yılı KKTC Resmi Tatilleri
INSERT INTO kktc_resmi_tatiller (tatil_adi, tatil_tarihi, tatil_turu, gun_adı, kacinci_gun, aciklama) VALUES
-- Ulusal / Resmi Tatiller
('Yılbaşı', '2026-01-01', 'Resmi', 'Perşembe', NULL, 'Yeni yıl tatili'),
('Ulusal Egemenlik ve Çocuk Bayramı', '2026-04-23', 'Ulusal', 'Perşembe', NULL, '23 Nisan Ulusal Egemenlik ve Çocuk Bayramı'),
('İşçi Bayramı', '2026-05-01', 'Resmi', 'Cuma', NULL, '1 Mayıs Emek ve Dayanışma Günü'),
('Gençlik ve Spor Bayramı', '2026-05-19', 'Ulusal', 'Salı', NULL, '19 Mayıs Atatürk''ü Anma, Gençlik ve Spor Bayramı'),
('Barış ve Özgürlük Bayramı', '2026-07-20', 'Ulusal', 'Pazartesi', NULL, '20 Temmuz Barış ve Özgürlük Bayramı'),
('TMT Günü', '2026-08-01', 'Ulusal', 'Cumartesi', NULL, 'Türk Mukavemet Teşkilatı Kuruluş Günü'),
('Zafer Bayramı', '2026-08-30', 'Ulusal', 'Pazar', NULL, '30 Ağustos Zafer Bayramı'),
('Cumhuriyet Bayramı', '2026-10-29', 'Ulusal', 'Perşembe', NULL, '29 Ekim Cumhuriyet Bayramı'),
('KKTC Cumhuriyet Günü', '2026-11-15', 'Ulusal', 'Pazar', NULL, '15 Kasım Kuzey Kıbrıs Türk Cumhuriyeti''nin İlanı'),
-- Dini Tatiller - Ramazan Bayramı
('Ramazan Bayramı Arife', '2026-03-19', 'Dini', 'Perşembe', NULL, 'Ramazan Bayramı arife günü (öğleden sonra tatil)'),
('Ramazan Bayramı', '2026-03-20', 'Dini', 'Cuma', '1. Gün', 'Ramazan Bayramı 1. gün'),
('Ramazan Bayramı', '2026-03-21', 'Dini', 'Cumartesi', '2. Gün', 'Ramazan Bayramı 2. gün'),
('Ramazan Bayramı', '2026-03-22', 'Dini', 'Pazar', '3. Gün', 'Ramazan Bayramı 3. gün'),
-- Dini Tatiller - Kurban Bayramı
('Kurban Bayramı Arife', '2026-05-26', 'Dini', 'Salı', NULL, 'Kurban Bayramı arife günü (öğleden sonra tatil)'),
('Kurban Bayramı', '2026-05-27', 'Dini', 'Çarşamba', '1. Gün', 'Kurban Bayramı 1. gün'),
('Kurban Bayramı', '2026-05-28', 'Dini', 'Perşembe', '2. Gün', 'Kurban Bayramı 2. gün'),
('Kurban Bayramı', '2026-05-29', 'Dini', 'Cuma', '3. Gün', 'Kurban Bayramı 3. gün'),
('Kurban Bayramı', '2026-05-30', 'Dini', 'Cumartesi', '4. Gün', 'Kurban Bayramı 4. gün');


-- =============================================
-- RESMİ TATİL SORGULARI (Uygulama seviyesi)
-- =============================================

-- [ResmiTatilController - Index]
-- Tüm tatilleri tarihe göre sıralı listele
-- SELECT id, tatil_adi, tatil_tarihi, tatil_turu, gun_adı, kacinci_gun, aciklama
-- FROM kktc_resmi_tatiller ORDER BY tatil_tarihi;

-- [PuantajController - GetResmiTatiller]
-- Belirli ay/yıl için resmi tatilleri getir (hakediş hesabı için)
-- SELECT DAY(tatil_tarihi) AS gun, tatil_adi FROM kktc_resmi_tatiller
-- WHERE YEAR(tatil_tarihi) = @yil AND MONTH(tatil_tarihi) = @ay
-- ORDER BY tatil_tarihi;
