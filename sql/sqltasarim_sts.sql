-- ============================================
-- STS Veritabanı Oluşturma Script'i
-- ============================================

-- 1. Veritabanını oluştur (eğer yoksa)
CREATE DATABASE IF NOT EXISTS sest_db 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_turkish_ci;

USE sest_db;

-- ============================================
-- 2. Tüm Tabloları Oluştur (stk_ ön ekli)
-- ============================================

-- 2.1. Şubeler Tablosu
CREATE TABLE IF NOT EXISTS stk_Sube (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Ad VARCHAR(100) NOT NULL,
    Kod VARCHAR(10) NOT NULL UNIQUE,
    Tip ENUM('Sube', 'AnaDepo') NOT NULL DEFAULT 'Sube',
    AktifMi BOOLEAN DEFAULT TRUE,
    SorumluKullaniciId INT NULL,
    OlusturmaTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_tip (Tip),
    INDEX idx_aktif (AktifMi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.2. Ürünler Tablosu
CREATE TABLE IF NOT EXISTS stk_Urun (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Ad VARCHAR(200) NOT NULL,
    Barkod VARCHAR(50) NULL UNIQUE,
    Birim VARCHAR(20) NOT NULL DEFAULT 'Adet',
    Firma VARCHAR(255) NULL,
    AktifMi BOOLEAN DEFAULT TRUE,
    INDEX idx_ad (Ad),
    INDEX idx_firma (Firma),
    INDEX idx_aktif (AktifMi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.3. Kullanıcılar Tablosu
CREATE TABLE IF NOT EXISTS stk_Kullanici (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SubeId INT NOT NULL,
    AdSoyad VARCHAR(100) NOT NULL,
    KullaniciAdi VARCHAR(50) NOT NULL UNIQUE,
    SifreHash VARCHAR(255) NOT NULL,
    Rol ENUM('SubePersoneli', 'DepoSorumlusu', 'Admin') NOT NULL DEFAULT 'SubePersoneli',
    SonGirisTarihi DATETIME NULL,
    AktifMi BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (SubeId) REFERENCES stk_Sube(Id) ON DELETE CASCADE,
    INDEX idx_subeid (SubeId),
    INDEX idx_rol (Rol),
    INDEX idx_aktif (AktifMi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.4. Eksik Kayıtları Tablosu
CREATE TABLE IF NOT EXISTS stk_EksikKaydi (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SubeId INT NOT NULL,
    UrunId INT NOT NULL,
    Miktar DECIMAL(10,2) NOT NULL CHECK (Miktar > 0),
    AcilMi BOOLEAN DEFAULT FALSE,
    Not VARCHAR(500) NULL,
    Durum ENUM('Bekliyor', 'SevkEdildi', 'Tamamlandi') NOT NULL DEFAULT 'Bekliyor',
    HaftaNo VARCHAR(10) NOT NULL,
    GirisTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    GirisiYapanKullaniciId INT NOT NULL,
    SonGuncellemeTarihi DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (SubeId) REFERENCES stk_Sube(Id) ON DELETE CASCADE,
    FOREIGN KEY (UrunId) REFERENCES stk_Urun(Id) ON DELETE CASCADE,
    FOREIGN KEY (GirisiYapanKullaniciId) REFERENCES stk_Kullanici(Id),
    UNIQUE KEY unique_hafta_sube_urun (HaftaNo, SubeId, UrunId),
    INDEX idx_hafta (HaftaNo),
    INDEX idx_durum (Durum),
    INDEX idx_acil (AcilMi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.5. Sevkiyat Tablosu
CREATE TABLE IF NOT EXISTS stk_Sevkiyat (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    EksikKaydiId INT NOT NULL,
    SevkMiktari DECIMAL(10,2) NOT NULL CHECK (SevkMiktari > 0),
    SevkTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    SevkedenKullaniciId INT NOT NULL,
    Durum ENUM('Hazirlaniyor', 'Yolda', 'TeslimEdildi', 'OnayBekliyor', 'Onaylandi') NOT NULL DEFAULT 'Hazirlaniyor',
    SubeOnayTarihi DATETIME NULL,
    SubeOnayNotu VARCHAR(500) NULL,
    FOREIGN KEY (EksikKaydiId) REFERENCES stk_EksikKaydi(Id) ON DELETE CASCADE,
    FOREIGN KEY (SevkedenKullaniciId) REFERENCES stk_Kullanici(Id),
    INDEX idx_durum (Durum),
    INDEX idx_sevktarihi (SevkTarihi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.6. Fabrika Siparişleri Tablosu
CREATE TABLE IF NOT EXISTS stk_FabrikaSiparisi (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    UrunId INT NOT NULL,
    Miktar DECIMAL(10,2) NOT NULL CHECK (Miktar > 0),
    HaftaNo VARCHAR(10) NOT NULL,
    SiparisTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    Durum ENUM('SiparisVerildi', 'Uretimde', 'Yolda', 'TeslimAlindi') NOT NULL DEFAULT 'SiparisVerildi',
    TahminiTeslimTarihi DATE NULL,
    Not VARCHAR(500) NULL,
    FOREIGN KEY (UrunId) REFERENCES stk_Urun(Id) ON DELETE CASCADE,
    INDEX idx_hafta (HaftaNo),
    INDEX idx_durum (Durum)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.7. Hareket Log Tablosu
CREATE TABLE IF NOT EXISTS stk_HareketLog (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    TabloAdi VARCHAR(50) NOT NULL,
    KayitId INT NOT NULL,
    EskiDeger TEXT NULL,
    YeniDeger TEXT NULL,
    IslemTipi ENUM('Ekle', 'Guncelle', 'Sil', 'Duzeltme') NOT NULL,
    YapanKullaniciId INT NOT NULL,
    IslemTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    IpAdres VARCHAR(45) NULL,
    FOREIGN KEY (YapanKullaniciId) REFERENCES stk_Kullanici(Id),
    INDEX idx_tablo_kayit (TabloAdi, KayitId),
    INDEX idx_islmtarihi (IslemTarihi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 2.8. Hafta Kapanış Tablosu (Geç giriş takibi)
CREATE TABLE IF NOT EXISTS stk_HaftaKapanis (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    HaftaNo VARCHAR(10) NOT NULL UNIQUE,
    PazartesiTarihi DATE NOT NULL,
    EksikGirisKapandiMi BOOLEAN DEFAULT FALSE,
    GecGirenSubeler JSON NULL,
    INDEX idx_hafta (HaftaNo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- ============================================
-- 3. Örnek Veriler
-- ============================================

-- Ana Depo ekle
INSERT IGNORE INTO stk_Sube (Ad, Kod, Tip) VALUES 
('Ana Depo', 'ANADEPO', 'AnaDepo');

-- Örnek şubeler ekle
INSERT IGNORE INTO stk_Sube (Ad, Kod, Tip) VALUES 
('Şube 1', 'SUB01', 'Sube'),
('Şube 2', 'SUB02', 'Sube'),
('Şube 3', 'SUB03', 'Sube');

-- Örnek ürünler ekle
INSERT IGNORE INTO stk_Urun (Ad, Barkod, Birim) VALUES 
('Bağlantı Halkası', '1001', 'Adet'),
('Metal Somun', '1002', 'Adet'),
('Demir Çubuk', '1003', 'Meter'),
('Rondela', '1004', 'Adet'),
('Ürün 5', '1005', 'Adet');

-- ============================================
-- 4. Önemli Notlar
-- ============================================
-- * Tüm tabloların stk_ prefix'i vardır
-- * UTF8MB4 kodlaması Türkçe karakterleri destekler
-- * UNIQUE key sayesinde aynı hafta/şube/ürün kombinasyonu sadece bir kez eklenebilir
-- * SubeId, UrunId ve diğer Foreign Keys CASCADE silmeye ayarlanmıştır
-- * Performans için gerekli indeksler oluşturulmuştur
-- ============================================
