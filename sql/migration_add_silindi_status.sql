-- ============================================
-- Silindi Durumu Ekleme Migration
-- ============================================

USE u2636310_dbE97;

-- stk_EksikKaydi tablosunun Durum enum'ına 'Silindi' ekle
ALTER TABLE stk_EksikKaydi 
MODIFY Durum ENUM('Bekliyor', 'SevkEdildi', 'Tamamlandi', 'Silindi') NOT NULL DEFAULT 'Bekliyor';

-- stk_Sevkiyat tablosunun Durum enum'ına 'Silindi' ekle
ALTER TABLE stk_Sevkiyat 
MODIFY Durum ENUM('Hazirlaniyor', 'Yolda', 'TeslimEdildi', 'OnayBekliyor', 'Onaylandi', 'IadeEdildi', 'Silindi') NOT NULL DEFAULT 'Hazirlaniyor';

-- stk_EksikKaydi tablosuna silme ile ilgili sütunlar ekle (soft delete ve log için)
ALTER TABLE stk_EksikKaydi 
ADD COLUMN IF NOT EXISTS SilindiMi BOOLEAN DEFAULT FALSE AFTER Durum,
ADD COLUMN IF NOT EXISTS SilmeSebebi VARCHAR(500) NULL AFTER SilindiMi,
ADD COLUMN IF NOT EXISTS SilmeTarihi DATETIME NULL AFTER SilmeSebebi,
ADD COLUMN IF NOT EXISTS SilenKullaniciId INT NULL AFTER SilmeTarihi,
ADD CONSTRAINT IF NOT EXISTS fk_stk_EksikKaydi_SilenKullaniciId FOREIGN KEY (SilenKullaniciId) REFERENCES stk_Kullanici(Id) ON DELETE SET NULL,
ADD INDEX IF NOT EXISTS idx_silindi (SilindiMi);
