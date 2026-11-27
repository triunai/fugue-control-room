-- Keyset page of customers after the cursor (oldest first); EF composes the filters on top. Caller: CustomerRepository.
SELECT id, name, email, phone, status, user_id, created_at, created_by, updated_at, updated_by, xmin
FROM customers WHERE (created_at, id) > (@createdAt, @id)
