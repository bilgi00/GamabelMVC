-- Şikayet tablosu MySQL şeması
CREATE TABLE `Sikayet` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Tarih` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `KullaniciId` INT NOT NULL,
  `KullaniciAdi` VARCHAR(100) NOT NULL,
  `Konu` VARCHAR(255) NOT NULL,
  `Detay` TEXT,
  `Sonuc` TEXT,
  `SonucTarihi` DATETIME NULL,
  `Durum` VARCHAR(20) NOT NULL DEFAULT 'Açık',
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Admin'e bildirim için öneri:
-- Şikayet eklendiğinde stk_Bildirim tablosuna yeni kayıt eklenebilir:
-- INSERT INTO stk_Bildirim (Baslik, Mesaj, Link, HedefRol, OkunduMu, OlusturmaTarihi)
-- VALUES ('Yeni Şikayet', CONCAT('Yeni şikayet: ', @Konu), '/Sikayet/Admin', 'admin', false, NOW());