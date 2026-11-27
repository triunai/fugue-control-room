-- Inserts a customer and returns its xmin as the first concurrency token. Caller: CustomerRepository.
INSERT INTO customers (id, name, email, phone, status, user_id, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @Name, @Email, @Phone, @Status, @UserId, @Now, @Actor, @Now, @Actor)
RETURNING xmin::text::bigint
