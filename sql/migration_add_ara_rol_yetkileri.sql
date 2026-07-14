-- Migration: DepoSorumlusu rolüne ek yetki alanı ekleme
-- Tarih: 2026-06-11
-- Açıklama: DepoSorumlusu rolü hem Depo Sorumlusu hem Şube Personeli yetkilerine sahip olabilsin diye ek alan ekleniyor

-- 1. stk_Kullanici tablosuna AraRolYetkileri sütunu ekle
ALTER TABLE stk_Kullanici ADD COLUMN AraRolYetkileri NVARCHAR(50) NULL DEFAULT NULL;

-- 2. Açıklama: 
-- AraRolYetkileri NULL = Sadece ana rolü (DepoSorumlusu = merkez işlemleri)
-- AraRolYetkileri = 'SubePersoneli' = DepoSorumlusu + SubePersoneli yetkileri

-- 3. DepoSorumlusu rolü hakkında açıklama (Tablo yapısı):
-- rol = 'DepoSorumlusu': Merkez operasyonları yapabiliyor (Sevkiyat, Sipariş, Raporlar)
-- rol = 'SubePersoneli': Sadece eksik kayıt giriş yapabiliyor (kendi şubesi)
-- rol = 'Admin': Tüm işlemler + veritabanı yönetimi

-- 4. Kontrol: Migration başarılı mı kontrol etmek için
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'stk_Kullanici' 
AND COLUMN_NAME IN ('Id', 'Rol', 'AraRolYetkileri');
