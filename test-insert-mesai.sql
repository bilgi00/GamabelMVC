-- Test mesai kaydı ekle (Temmuz 2026, AÇIK PAZAR)
USE u2636310_dbE97;

-- Personel ekle (eğer yoksa)
INSERT IGNORE INTO personeller (ad, soyad, birim_adi, per_statu) 
VALUES ('Test', 'Kişi', 'AÇIK PAZAR', 'aktif');

-- Eklenen personelin ID'sini al
SET @personel_id = (SELECT id FROM personeller WHERE ad = 'Test' AND soyad = 'Kişi' LIMIT 1);

-- Mesai kaydı ekle (Temmuz 2026)
INSERT INTO mesai_kayitlari (personel_id, tarih, toplam_saat, not)
VALUES (@personel_id, '2026-07-05', 2.5, 'Test mesai kaydı');

-- Mesai saati brüt ekle
INSERT IGNORE INTO mesaisaat (saatlik_brut, tarih) 
VALUES (100.00, NOW());

SELECT 'Test verisi eklendi' AS Sonuc;
