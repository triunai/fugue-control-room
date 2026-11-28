-- The ETag of each user on a page: xmin, the row version Postgres bumps on every UPDATE (not mapped in the EF model). Caller: UserDirectory.
SELECT id, xmin::text::bigint AS version FROM users WHERE id = ANY(@ids)
