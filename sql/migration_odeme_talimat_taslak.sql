CREATE TABLE IF NOT EXISTS prs_ot_taslak_talimatlar (
    id                   INT AUTO_INCREMENT PRIMARY KEY,
    talimat_tarihi       DATE NULL,
    banka_id             INT NULL,
    hazirlayan_kullanici VARCHAR(100) NOT NULL,
    olusturma_tarihi     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    guncelleme_tarihi    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_ot_taslak_guncelleme (guncelleme_tarihi)
);

CREATE TABLE IF NOT EXISTS prs_ot_taslak_talimat_satirlari (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    taslak_id   INT NOT NULL,
    sira        INT NOT NULL,
    firma_id    INT NULL,
    fatura_adi  VARCHAR(200) NULL,
    tutar       TEXT NULL,
    INDEX idx_ot_taslak_satir_sira (taslak_id, sira)
);
