-- personeller.per_no anahtar geçişi
-- Çalıştırmadan önce per_no alanında NULL veya tekrar olmadığından emin olun.

SET @null_count = (SELECT COUNT(*) FROM personeller WHERE per_no IS NULL OR TRIM(per_no) = '');
SET @duplicate_count = (
    SELECT COUNT(*)
    FROM (
        SELECT per_no
        FROM personeller
        GROUP BY per_no
        HAVING COUNT(*) > 1
    ) duplicates
);

DELIMITER //
CREATE PROCEDURE validate_personel_per_no()
BEGIN
    IF @null_count > 0 OR @duplicate_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'personeller.per_no NULL/boş veya tekrar eden değerler içeriyor; migration durduruldu.';
    END IF;
END//
CALL validate_personel_per_no()//
DROP PROCEDURE validate_personel_per_no//
DELIMITER ;

-- Eski INT foreign key değerlerini per_no değerlerine dönüştür.
ALTER TABLE puantaj_izin ADD COLUMN personel_id_new VARCHAR(50) NULL;
ALTER TABLE mesai_kayitlari ADD COLUMN personel_id_new VARCHAR(50) NULL;

UPDATE puantaj_izin pi
INNER JOIN personeller p ON p.id = pi.personel_id
SET pi.personel_id_new = p.per_no;

UPDATE mesai_kayitlari mk
INNER JOIN personeller p ON p.id = mk.personel_id
SET mk.personel_id_new = p.per_no;

DELIMITER //
CREATE PROCEDURE validate_personel_references()
BEGIN
        IF EXISTS (SELECT 1 FROM puantaj_izin WHERE personel_id_new IS NULL)
             OR EXISTS (SELECT 1 FROM mesai_kayitlari WHERE personel_id_new IS NULL) THEN
                SIGNAL SQLSTATE '45000'
                        SET MESSAGE_TEXT = 'Mesai/puantaj kaydında eşleşmeyen personeller bulundu; migration durduruldu.';
        END IF;
END//
CALL validate_personel_references()//
DROP PROCEDURE validate_personel_references//
DELIMITER ;

-- Foreign key adları kurulum ortamına göre değişebileceğinden metadata üzerinden kaldırılır.
SET @fk = (
        SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'puantaj_izin'
            AND COLUMN_NAME = 'personel_id' AND REFERENCED_TABLE_NAME = 'personeller'
        LIMIT 1
);
SET @sql = IF(@fk IS NULL, 'SELECT 1', CONCAT('ALTER TABLE puantaj_izin DROP FOREIGN KEY `', @fk, '`'));
PREPARE drop_fk FROM @sql;
EXECUTE drop_fk;
DEALLOCATE PREPARE drop_fk;

SET @fk = (
        SELECT CONSTRAINT_NAME FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'mesai_kayitlari'
            AND COLUMN_NAME = 'personel_id' AND REFERENCED_TABLE_NAME = 'personeller'
        LIMIT 1
);
SET @sql = IF(@fk IS NULL, 'SELECT 1', CONCAT('ALTER TABLE mesai_kayitlari DROP FOREIGN KEY `', @fk, '`'));
PREPARE drop_fk FROM @sql;
EXECUTE drop_fk;
DEALLOCATE PREPARE drop_fk;

ALTER TABLE puantaj_izin DROP COLUMN personel_id;
ALTER TABLE mesai_kayitlari DROP COLUMN personel_id;
ALTER TABLE puantaj_izin CHANGE COLUMN personel_id_new personel_id VARCHAR(50) NOT NULL;
ALTER TABLE mesai_kayitlari CHANGE COLUMN personel_id_new personel_id VARCHAR(50) NOT NULL;

ALTER TABLE personeller ADD UNIQUE KEY uq_personel_id (id);
ALTER TABLE personeller DROP PRIMARY KEY;
ALTER TABLE personeller DROP INDEX uq_per_no;
ALTER TABLE personeller MODIFY COLUMN per_no VARCHAR(50) NOT NULL;
ALTER TABLE personeller ADD PRIMARY KEY (per_no);

ALTER TABLE puantaj_izin
    ADD INDEX idx_personel (personel_id),
    ADD CONSTRAINT fk_puantaj_personel FOREIGN KEY (personel_id) REFERENCES personeller(per_no) ON DELETE CASCADE;

ALTER TABLE mesai_kayitlari
    ADD INDEX idx_personel (personel_id),
    ADD CONSTRAINT fk_mesai_personel FOREIGN KEY (personel_id) REFERENCES personeller(per_no) ON DELETE CASCADE;