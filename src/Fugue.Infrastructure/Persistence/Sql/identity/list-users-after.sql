-- Keyset page of users after the cursor (oldest first); EF composes the live-user and search filters on top. Caller: UserDirectory.
SELECT id, tenant_id, display_name, email, contact_number, is_active, local_login_enabled, password_hash, password_last_changed_at,
created_at, created_by, updated_at, updated_by, is_deleted, deleted_at, deleted_by
FROM users WHERE (created_at, id) > (@createdAt, @id)
