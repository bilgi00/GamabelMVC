-- ============================================
-- Sistem Denetim Günlükleri İçin Veritabanı Güncelleme
-- ============================================

USE sest_db;

-- 1. stk_HareketLog tablosuna sütun ekle (eğer yoksa)
ALTER TABLE stk_HareketLog 
ADD COLUMN IF NOT EXISTS BilgisayarAdi VARCHAR(255) NULL AFTER IpAdres,
ADD COLUMN IF NOT EXISTS MacAdresi VARCHAR(17) NULL AFTER BilgisayarAdi;

-- 2. Kullanıcı Giriş Günlüğü Tablosu
CREATE TABLE IF NOT EXISTS stk_GirisGunlugu (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    KullaniciId INT NOT NULL,
    GirisTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    IpAdresi VARCHAR(45) NULL,
    BilgisayarAdi VARCHAR(255) NULL,
    MacAdresi VARCHAR(17) NULL,
    Tarayici VARCHAR(500) NULL,
    IsletimSistemi VARCHAR(255) NULL,
    BasariliMi BOOLEAN DEFAULT TRUE,
    HataMesaji VARCHAR(500) NULL,
    CikisTarihi DATETIME NULL,
    SureTersin INT NULL COMMENT 'Dakika cinsinden',
    FOREIGN KEY (KullaniciId) REFERENCES stk_Kullanici(Id) ON DELETE CASCADE,
    INDEX idx_kullaniciid (KullaniciId),
    INDEX idx_tarih (GirisTarihi),
    INDEX idx_basarili (BasariliMi)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- 3. Sipariş İşlemleri Denetim Günlüğü
CREATE TABLE IF NOT EXISTS stk_SiparisAuditLog (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SiparisId INT NOT NULL,
    IslemTipi ENUM('Olustur', 'Guncelle', 'Sil', 'DurumDegistir') NOT NULL,
    YapanKullaniciId INT NOT NULL,
    IslemTarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    IpAdresi VARCHAR(45) NULL,
    BilgisayarAdi VARCHAR(255) NULL,
    MacAdresi VARCHAR(17) NULL,
    EskiDegerler JSON NULL COMMENT 'Güncelleme sırasında önceki değerler',
    YeniDegerler JSON NULL COMMENT 'Yeni değerler',
    Notlar VARCHAR(500) NULL,
    FOREIGN KEY (YapanKullaniciId) REFERENCES stk_Kullanici(Id),
    INDEX idx_siparisid (SiparisId),
    INDEX idx_tarihi (IslemTarihi),
    INDEX idx_kullaniciid (YapanKullaniciId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

-- ============================================
-- Örnek Sorgu: Giriş günlüğünü kontrol etme
-- ============================================
-- SELECT * FROM stk_GirisGunlugu WHERE KullaniciId = 1 ORDER BY GirisTarihi DESC LIMIT 20;

-- ============================================
-- Örnek Sorgu: Sipariş işlemlerini kontrol etme
-- ============================================
-- SELECT * FROM stk_SiparisAuditLog WHERE SiparisId = 1 ORDER BY IslemTarihi DESC;
