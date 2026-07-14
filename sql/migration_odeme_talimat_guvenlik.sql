-- Mevcut kurulumlar icin odeme talimat guvenlik gecisi.
-- Once ayni talimat_no ile yinelenen kayit olmadigini dogrulayin.

CREATE TABLE IF NOT EXISTS prs_ot_talimat_siralari (
    yil      SMALLINT PRIMARY KEY,
    son_sira INT NOT NULL DEFAULT 0
);

SET @index_exists := (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'prs_ot_talimatlar'
      AND index_name = 'idx_ot_talimat_no'
);
SET @sql := IF(
    @index_exists = 0,
    'ALTER TABLE prs_ot_talimatlar ADD UNIQUE INDEX idx_ot_talimat_no (talimat_no)',
    'SELECT 1'
);
PREPARE statement FROM @sql;
EXECUTE statement;
DEALLOCATE PREPARE statement;
