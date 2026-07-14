-- Test user for PRS system
INSERT IGNORE INTO admin_kullanicilar (kullanici_adi, sifre, birim, rol, aktif_mi) 
VALUES ('test', 'test', 'AÇIK PAZAR', 'birim_amiri', 1);

-- Verify
SELECT id, kullanici_adi, birim, rol FROM admin_kullanicilar WHERE kullanici_adi = 'test';
