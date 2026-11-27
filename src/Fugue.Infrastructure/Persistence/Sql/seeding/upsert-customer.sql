-- Idempotent upsert of one seeded customer (ADR-006 upsert path). Caller: CustomerSeeder.
INSERT INTO customers (id, name, email, phone, status, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @Name, @Email, @Phone, @Status, @CreatedAt, @Actor, @Now, @Actor)
ON CONFLICT (id) DO UPDATE SET
  name = EXCLUDED.name, email = EXCLUDED.email, phone = EXCLUDED.phone, status = EXCLUDED.status,
  updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by
