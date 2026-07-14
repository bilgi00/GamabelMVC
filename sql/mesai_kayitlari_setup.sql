-- ============================================
-- HIZLI MESAI GİRİŞİ SİSTEMİ - VERİTABANI SETUP
-- ============================================
-- Bu SQL scripti, Hızlı Mesai Girişi sisteminin 
-- çalışması için gerekli olan veritabanı tablosunu oluşturur.
-- 
-- ⚠️ UYARI: Bu dosyada yazılan SQL kodu MySQL yöneticisi tarafından
-- manuel olarak çalıştırılması gerekir. Sistem kodu bunu otomatik
-- olarak çalıştırmaz.
-- ============================================

-- 1. VERİTABANI SEÇME
USE sest_db;

-- 2. MESAI KAYITLARI TABLOSU OLUŞTURMA
CREATE TABLE IF NOT EXISTS mesai_kayitlari (
    Id INT PRIMARY KEY AUTO_INCREMENT COMMENT 'Kayıt ID',
    KullaniciId INT NOT NULL COMMENT 'Kullanıcı ID (FK)',
    Tarih DATE NOT NULL COMMENT 'Mesai tarihi',
    BaslangicSaati TIME NOT NULL COMMENT 'Başlangıç saati (HH:MM)',
    BitisSaati TIME NOT NULL COMMENT 'Bitiş saati (HH:MM)',
    
    -- GENERATED olarak toplam saat hesaplaması
    ToplamSaat DECIMAL(5,2) GENERATED ALWAYS AS (
        ROUND(TIMESTAMPDIFF(MINUTE, BaslangicSaati, BitisSaati) / 60.0, 2)
    ) STORED COMMENT 'Otomatik hesaplanan toplam saat',
    
    Notlar VARCHAR(500) NULL COMMENT 'İsteğe bağlı notlar',
    
    OnaySuresi INT DEFAULT 0 COMMENT 'Onay süresi (dakika)',
    Durum ENUM('Bekliyor', 'Onaylandi', 'Reddedildi') DEFAULT 'Bekliyor' 
        COMMENT 'Mesai kaydının durumu',
    
    OnayanKullaniciId INT NULL COMMENT 'Onay yapan kullanıcı ID',
    OnaySuresiTarihi DATETIME NULL COMMENT 'Onay/Red tarihi',
    OlusturmaTarihi DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT 'Kayıt oluşturulma tarihi',
    
    -- FOREIGN KEYS
    FOREIGN KEY (KullaniciId) REFERENCES stk_Kullanici(Id) ON DELETE CASCADE,
    FOREIGN KEY (OnayanKullaniciId) REFERENCES stk_Kullanici(Id) ON DELETE SET NULL,
    
    -- CONSTRAINTS
    UNIQUE KEY unique_personel_gun (KullaniciId, Tarih) 
        COMMENT 'Bir kullanıcı günde sadece bir mesai kaydı yapabilir',
    
    -- INDEXES
    INDEX idx_tarih (Tarih) COMMENT 'Tarih sorgularında hızlı arama',
    INDEX idx_durum (Durum) COMMENT 'Durum filtrelemesinde hızlı arama',
    INDEX idx_kullanici (KullaniciId) COMMENT 'Kullanıcı sorgularında hızlı arama',
    INDEX idx_onayadurum (Durum, OnaySuresiTarihi) COMMENT 'Onay panelinde hızlı filtreleme'
    
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci
  COMMENT='Çalışanların ek mesai kaydını tutar';

-- ============================================
-- 3. ÖRNEK VERİ EKLEME (Opsiyonel)
-- ============================================
-- İlk kullanıcı kaydı için örnek veri
-- Not: Gerçek kullanıcı ID'lerle değiştirilmesi gerekir

INSERT INTO mesai_kayitlari (KullaniciId, Tarih, BaslangicSaati, BitisSaati, Notlar, Durum)
VALUES 
    (1, '2026-05-21', '09:00', '17:00', 'Normal çalışma', 'Onaylandi'),
    (1, '2026-05-22', '18:00', '20:30', 'Acil proje', 'Bekliyor'),
    (2, '2026-05-20', '10:00', '18:00', 'Stok kontrolü', 'Onaylandi');

-- ============================================
-- 4. VERİTABANI İSTATİSTİKLERİ
-- ============================================
-- Aşağıdaki sorgularla sistem hakkında bilgi edinebilirsiniz:

-- Tüm mesai kayıtlarını görmek
-- SELECT * FROM mesai_kayitlari;

-- Bekleyen mesai kayıtlarını görmek
-- SELECT * FROM mesai_kayitlari WHERE Durum = 'Bekliyor' ORDER BY OlusturmaTarihi DESC;

-- Kullanıcıya göre aylık mesai toplamı
-- SELECT 
--     k.AdSoyad,
--     MONTH(mk.Tarih) as Ay,
--     YEAR(mk.Tarih) as Yil,
--     SUM(mk.ToplamSaat) as ToplamSaat
-- FROM mesai_kayitlari mk
-- JOIN stk_Kullanici k ON mk.KullaniciId = k.Id
-- WHERE mk.Durum = 'Onaylandi'
-- GROUP BY k.AdSoyad, YEAR(mk.Tarih), MONTH(mk.Tarih)
-- ORDER BY Yil DESC, Ay DESC;

-- 20 saati aşan kullanıcılar
-- SELECT 
--     k.AdSoyad,
--     s.Ad as SubeAdi,
--     MONTH(mk.Tarih) as Ay,
--     SUM(mk.ToplamSaat) as ToplamSaat
-- FROM mesai_kayitlari mk
-- JOIN stk_Kullanici k ON mk.KullaniciId = k.Id
-- JOIN stk_Sube s ON k.SubeId = s.Id
-- WHERE mk.Durum = 'Onaylandi'
-- GROUP BY k.AdSoyad, MONTH(mk.Tarih), YEAR(mk.Tarih)
-- HAVING SUM(mk.ToplamSaat) > 20
-- ORDER BY ToplamSaat DESC;

-- ============================================
-- 5. TARİHÇE VE YÖNETİM
-- ============================================
-- Sistem oluşturulma tarihi: 21 Mayıs 2026
-- Son güncelleme: 21 Mayıs 2026
-- Version: 1.0

-- ============================================
-- KURULUM TAMAMLANDI ✅
-- ============================================
