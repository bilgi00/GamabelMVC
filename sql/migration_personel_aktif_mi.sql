-- Personel aktif/pasif durumu
ALTER TABLE personeller
    ADD COLUMN IF NOT EXISTS aktif_mi TINYINT(1) NOT NULL DEFAULT 1;
