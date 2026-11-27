-- Records one imported source item; a key already present inserts nothing (0 rows), which the caller reports as already imported. Caller: ImportStore.
INSERT INTO claim_imports (key, claim_id, source, file_name, file_sha256, item, imported_by, created_at, created_by, updated_at, updated_by)
VALUES (@Key, @ClaimId, @Source, @FileName, @FileSha256, @Item, @ImportedBy, @Now, @Actor, @Now, @Actor)
ON CONFLICT (key) DO NOTHING
