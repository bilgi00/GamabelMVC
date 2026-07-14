-- Sevkiyat Firma Alanı Ekleme - Migration
-- Bu script stk_Urun tablosuna Firma alanını ekler

ALTER TABLE stk_Urun ADD COLUMN Firma VARCHAR(255) NULL AFTER Birim;

-- Firma sütununun indeksini oluştur
CREATE INDEX idx_firma ON stk_Urun(Firma);

-- Örnek veri ekle (test için)
-- UPDATE stk_Urun SET Firma = 'Test Firma' WHERE Firma IS NULL LIMIT 5;
