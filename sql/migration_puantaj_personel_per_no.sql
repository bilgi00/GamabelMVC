-- Puantaj foreign key migration:
-- puantaj_izin.personel_id currently stores personeller.id and will store personeller.per_no.
-- Back up the database before running. This migration changes puantaj_izin only.
-- Run once, in the target database, with a MySQL client that supports DELIMITER.

SET @per_no_null_count = (
    SELECT COUNT(*) FROM personeller WHERE per_no IS NULL OR TRIM(per_no) = ''
);
SET @per_no_duplicate_count = (
    SELECT COUNT(*)
    FROM (
        SELECT per_no
        FROM personeller
        GROUP BY per_no
        HAVING COUNT(*) > 1
    ) duplicates
);
SET @per_no_data_type = (
    SELECT DATA_TYPE
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'personeller'
      AND COLUMN_NAME = 'per_no'
);
SET @per_no_column_type = (
    SELECT COLUMN_TYPE
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'personeller'
      AND COLUMN_NAME = 'per_no'
);
SET @per_no_charset = (
    SELECT CHARACTER_SET_NAME
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'personeller'
      AND COLUMN_NAME = 'per_no'
);
SET @per_no_collation = (
    SELECT COLLATION_NAME
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'personeller'
      AND COLUMN_NAME = 'per_no'
);

DROP PROCEDURE IF EXISTS validate_puantaj_per_no_migration;
DELIMITER //
CREATE PROCEDURE validate_puantaj_per_no_migration()
BEGIN
    IF @per_no_null_count > 0 OR @per_no_duplicate_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Migration durduruldu: personeller.per_no NULL, bos veya tekrar eden deger iceriyor.';
    END IF;

    IF @per_no_data_type NOT IN ('varchar', 'char')
       OR @per_no_column_type IS NULL
       OR @per_no_charset IS NULL
       OR @per_no_collation IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Migration durduruldu: personeller.per_no CHAR/VARCHAR ve karakter seti bilgisi olmali.';
    END IF;
END//
CALL validate_puantaj_per_no_migration()//
DROP PROCEDURE validate_puantaj_per_no_migration//
DELIMITER ;

SET @sql = CONCAT(
    'ALTER TABLE puantaj_izin ADD COLUMN personel_id_per_no ',
    @per_no_column_type,
    ' CHARACTER SET ', @per_no_charset,
    ' COLLATE ', @per_no_collation,
    ' NULL'
);
PREPARE add_per_no_column FROM @sql;
EXECUTE add_per_no_column;
DEALLOCATE PREPARE add_per_no_column;

UPDATE puantaj_izin pi
INNER JOIN personeller p ON p.id = pi.personel_id
SET pi.personel_id_per_no = p.per_no;

SET @unmatched_count = (
    SELECT COUNT(*) FROM puantaj_izin WHERE personel_id_per_no IS NULL
);
DROP PROCEDURE IF EXISTS validate_puantaj_per_no_mapping;
DELIMITER //
CREATE PROCEDURE validate_puantaj_per_no_mapping()
BEGIN
    IF @unmatched_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Migration durduruldu: puantaj kayitlari personeller.id ile eslesmedi; eski sutun korundu.';
    END IF;
END//
CALL validate_puantaj_per_no_mapping()//
DROP PROCEDURE validate_puantaj_per_no_mapping//
DELIMITER ;

SET @fk_name = (
    SELECT CONSTRAINT_NAME
    FROM information_schema.KEY_COLUMN_USAGE
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'puantaj_izin'
      AND COLUMN_NAME = 'personel_id'
      AND REFERENCED_TABLE_NAME = 'personeller'
    LIMIT 1
);
SET @sql = IF(
    @fk_name IS NULL,
    'SELECT 1',
    CONCAT('ALTER TABLE puantaj_izin DROP FOREIGN KEY `', @fk_name, '`')
);
PREPARE drop_puantaj_fk FROM @sql;
EXECUTE drop_puantaj_fk;
DEALLOCATE PREPARE drop_puantaj_fk;

SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'puantaj_izin'
          AND INDEX_NAME = 'uq_personel_gun'
    ),
    'ALTER TABLE puantaj_izin DROP INDEX uq_personel_gun',
    'SELECT 1'
);
PREPARE drop_unique_index FROM @sql;
EXECUTE drop_unique_index;
DEALLOCATE PREPARE drop_unique_index;

SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'puantaj_izin'
          AND INDEX_NAME = 'idx_personel'
    ),
    'ALTER TABLE puantaj_izin DROP INDEX idx_personel',
    'SELECT 1'
);
PREPARE drop_personel_index FROM @sql;
EXECUTE drop_personel_index;
DEALLOCATE PREPARE drop_personel_index;

ALTER TABLE puantaj_izin DROP COLUMN personel_id;
SET @sql = CONCAT(
    'ALTER TABLE puantaj_izin CHANGE COLUMN personel_id_per_no personel_id ',
    @per_no_column_type,
    ' CHARACTER SET ', @per_no_charset,
    ' COLLATE ', @per_no_collation,
    ' NOT NULL'
);
PREPARE rename_per_no_column FROM @sql;
EXECUTE rename_per_no_column;
DEALLOCATE PREPARE rename_per_no_column;

SET @per_no_unique_index_count = (
    SELECT COUNT(*)
    FROM (
        SELECT INDEX_NAME
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'personeller'
          AND COLUMN_NAME = 'per_no'
          AND NON_UNIQUE = 0
        GROUP BY INDEX_NAME
        HAVING COUNT(*) = 1
           AND MAX(SEQ_IN_INDEX) = 1
    ) unique_indexes
);
SET @sql = IF(
    @per_no_unique_index_count = 0,
    'ALTER TABLE personeller ADD UNIQUE KEY uq_personeller_per_no_fk (per_no)',
    'SELECT 1'
);
PREPARE ensure_per_no_unique FROM @sql;
EXECUTE ensure_per_no_unique;
DEALLOCATE PREPARE ensure_per_no_unique;

ALTER TABLE puantaj_izin
    ADD INDEX idx_personel (personel_id),
    ADD UNIQUE KEY uq_personel_gun (personel_id, yil, ay, gun),
    ADD CONSTRAINT fk_puantaj_personel
        FOREIGN KEY (personel_id) REFERENCES personeller(per_no)
        ON DELETE CASCADE;
