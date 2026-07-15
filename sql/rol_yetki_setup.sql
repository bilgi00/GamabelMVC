-- =========================================================
-- GAMABELMVC - ROL / YETKI YAPISI (MySQL)
-- =========================================================
-- Bu dosya, PRS tarafındaki menü ve ekran erişimlerini
-- veritabanındaki roll tablosundan yönetmek için kullanılır.
-- =========================================================

-- Tabloyu oluştur
CREATE TABLE IF NOT EXISTS roll (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ad VARCHAR(100) NOT NULL UNIQUE,
    aciklama VARCHAR(255),
    aktif_mi TINYINT(1) DEFAULT 1,
    menu_kullanici_yonetimi TINYINT(1) DEFAULT 0,
    menu_personel TINYINT(1) DEFAULT 0,
    menu_puantaj TINYINT(1) DEFAULT 0,
    menu_rapor TINYINT(1) DEFAULT 0,
    menu_mesai TINYINT(1) DEFAULT 0,
    menu_tatiller TINYINT(1) DEFAULT 0,
    menu_odeme_talimat TINYINT(1) DEFAULT 0,
    menu_firma_yonetimi TINYINT(1) DEFAULT 0,
    menu_banka_yonetimi TINYINT(1) DEFAULT 0,
    menu_yetkilendirme TINYINT(1) DEFAULT 0,
    menu_dokumantasyon TINYINT(1) DEFAULT 0,
    menu_sikayet_admin TINYINT(1) DEFAULT 0,
    olusturma_tarihi DATETIME DEFAULT CURRENT_TIMESTAMP,
    guncelleme_tarihi DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Varsayılan rolleri ekle
INSERT INTO roll (ad, aciklama, aktif_mi,
    menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai,
    menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi,
    menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
SELECT 'admin', 'Sistem yöneticisi', 1,
       1, 1, 1, 1, 1,
       1, 1, 1, 1,
       1, 1, 1
WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'admin');

INSERT INTO roll (ad, aciklama, aktif_mi,
    menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai,
    menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi,
    menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
SELECT 'birim_amiri', 'Birim amiri', 1,
       0, 1, 1, 1, 1,
       1, 1, 0, 0,
       0, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'birim_amiri');

INSERT INTO roll (ad, aciklama, aktif_mi,
    menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai,
    menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi,
    menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
SELECT 'sube_personeli', 'Şube personeli', 1,
       0, 0, 0, 0, 0,
       0, 0, 0, 0,
       0, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'sube_personeli');

INSERT INTO roll (ad, aciklama, aktif_mi,
    menu_kullanici_yonetimi, menu_personel, menu_puantaj, menu_rapor, menu_mesai,
    menu_tatiller, menu_odeme_talimat, menu_firma_yonetimi, menu_banka_yonetimi,
    menu_yetkilendirme, menu_dokumantasyon, menu_sikayet_admin)
SELECT 'depo_sorumlusu', 'Depo sorumlusu', 1,
       0, 0, 0, 0, 0,
       0, 0, 0, 0,
       0, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM roll WHERE LOWER(ad) = 'depo_sorumlusu');
