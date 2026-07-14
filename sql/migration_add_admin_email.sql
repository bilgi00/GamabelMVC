-- ============================================
-- Email Sütunu Ekleme Migration
-- ============================================

USE u2636310_dbE97;

-- admin_kullanicilar tablosuna email sütunu ekle (eğer yoksa)
ALTER TABLE admin_kullanicilar
ADD COLUMN IF NOT EXISTS email VARCHAR(255) NULL COMMENT 'Admin kullanıcısının email adresi (raporlar için)';

-- Email sütununa index ekle
ALTER TABLE admin_kullanicilar
ADD INDEX IF NOT EXISTS idx_email (email);

-- ============================================
-- STS Database (Same as above)
-- ============================================

-- Migrasyonlar aynı veritabanında yapılıyor

-- Eğer admin_kullanicilar tablosu STS'de de varsa, email sütunu ekle
ALTER TABLE admin_kullanicilar
ADD COLUMN IF NOT EXISTS email VARCHAR(255) NULL COMMENT 'Admin kullanıcısının email adresi (raporlar için)';

-- Email sütununa index ekle
ALTER TABLE admin_kullanicilar
ADD INDEX IF NOT EXISTS idx_email (email);
