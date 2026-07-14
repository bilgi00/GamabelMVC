-- Insert test user for STS system
-- Username: depo1, Password will be hashed with BCrypt(123456)

-- First, get Ana Depo ID
SET @SubeId = (SELECT Id FROM stk_Sube WHERE Kod = 'ANADEPO' LIMIT 1);

-- Then insert the test user
INSERT IGNORE INTO stk_Kullanici (SubeId, AdSoyad, KullaniciAdi, SifreHash, Rol, AktifMi)
VALUES (@SubeId, 'Test Depo Sorumlusu', 'depo1', '$2a$11$UqcRGJQh7e6w8N6y5X3Q4uLzKdFD3K7F8Q0Z3R2C1P9O8N7M6L5K', 'DepoSorumlusu', 1);

-- Verify insertion
SELECT * FROM stk_Kullanici WHERE KullaniciAdi = 'depo1';
