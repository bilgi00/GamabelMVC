-- ============================================================
-- PRS - Ödeme Talimat Sistemi MySQL Şeması
-- Çalıştırma: Bu dosyayı mevcut gamabel veritabanına uygulayın
-- ============================================================

-- Şirketin ödeme yapacağı banka/hesaplar (eski Excel: Bankalar sayfası)
CREATE TABLE IF NOT EXISTS prs_ot_bankalar (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    sube_adi   VARCHAR(100) NOT NULL,
    iban       VARCHAR(40)  NOT NULL
);

-- Alıcı firmalar ve IBAN bilgisi (eski Excel: Firmalar sayfası / VLOOKUP kaynağı)
CREATE TABLE IF NOT EXISTS prs_ot_firmalar (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    cari_ismi   VARCHAR(250) NOT NULL,        -- Faturadaki / açık fatura listesindeki isim
    odeme_ismi  VARCHAR(250) NOT NULL,        -- IBAN sahibinin gerçek adı
    iban        VARCHAR(40)  NOT NULL,
    aciklama    VARCHAR(250),
    UNIQUE INDEX idx_cari_ismi (cari_ismi)
);

-- Yüklenen her Excel dosyasının kaydı
CREATE TABLE IF NOT EXISTS prs_ot_import_batchlari (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    dosya_adi       VARCHAR(500) NOT NULL,
    yukleme_tarihi  DATETIME     NOT NULL DEFAULT NOW(),
    satir_sayisi    INT          NOT NULL DEFAULT 0
);

-- Excel'den okunan açık fatura satırları
CREATE TABLE IF NOT EXISTS prs_ot_acik_faturalar (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    cari_kart               VARCHAR(250) NOT NULL,
    fatura_no               VARCHAR(100) NOT NULL,
    bakiye                  DECIMAL(18,2) NOT NULL DEFAULT 0,
    odemeye_dahil_edildi    TINYINT(1)   NOT NULL DEFAULT 0,
    import_batch_id         INT          NOT NULL,
    INDEX idx_cari_fatura   (cari_kart, fatura_no),
    CONSTRAINT fk_ot_fatura_batch FOREIGN KEY (import_batch_id)
        REFERENCES prs_ot_import_batchlari(id) ON DELETE CASCADE
);

-- Ödeme talimatı başlık bilgisi
CREATE TABLE IF NOT EXISTS prs_ot_talimatlar (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    talimat_no              VARCHAR(50)  NOT NULL,
    tarih                   DATETIME     NOT NULL DEFAULT NOW(),
    banka_id                INT          NOT NULL,
    toplam_tutar            DECIMAL(18,2) NOT NULL DEFAULT 0,
    toplam_adet             INT          NOT NULL DEFAULT 0,
    hazirlayan_kullanici    VARCHAR(100) NOT NULL,
    onaylayan_kullanici     VARCHAR(100),
    UNIQUE INDEX idx_ot_talimat_no (talimat_no),
    CONSTRAINT fk_ot_talimat_banka FOREIGN KEY (banka_id)
        REFERENCES prs_ot_bankalar(id)
);

CREATE TABLE IF NOT EXISTS prs_ot_talimat_siralari (
    yil         SMALLINT PRIMARY KEY,
    son_sira    INT NOT NULL DEFAULT 0
);

-- Talimat satırları (firma başına bir satır, birden fazla fatura toplanır)
CREATE TABLE IF NOT EXISTS prs_ot_talimat_satirlari (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    talimat_id  INT           NOT NULL,
    firma_id    INT           NOT NULL,
    aciklama    TEXT          NOT NULL,
    tutar       DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT fk_ot_satir_talimat FOREIGN KEY (talimat_id)
        REFERENCES prs_ot_talimatlar(id) ON DELETE CASCADE,
    CONSTRAINT fk_ot_satir_firma FOREIGN KEY (firma_id)
        REFERENCES prs_ot_firmalar(id)
);

-- Talimat satırı ile kaynak fatura arasındaki N:M ilişkisi
CREATE TABLE IF NOT EXISTS prs_ot_talimat_satiri_faturalari (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    talimat_satiri_id   INT NOT NULL,
    acik_fatura_id      INT NOT NULL,
    UNIQUE INDEX idx_satir_fatura (talimat_satiri_id, acik_fatura_id),
    CONSTRAINT fk_ot_tsf_satir FOREIGN KEY (talimat_satiri_id)
        REFERENCES prs_ot_talimat_satirlari(id) ON DELETE CASCADE,
    CONSTRAINT fk_ot_tsf_fatura FOREIGN KEY (acik_fatura_id)
        REFERENCES prs_ot_acik_faturalar(id) ON DELETE CASCADE
);

-- ============================================================
-- Örnek başlangıç verisi (kendi verilerinizle değiştirin)
-- ============================================================
-- INSERT INTO prs_ot_bankalar (sube_adi, iban) VALUES
--     ('GARANTİ BBVA - Gazimağusa', 'TR000000000000000000000001');
--
-- INSERT INTO prs_ot_firmalar (cari_ismi, odeme_ismi, iban) VALUES
--     ('ABC TİCARET A.Ş.', 'ABC TİCARET ANONİM ŞİRKETİ', 'TR000000000000000000000002');
