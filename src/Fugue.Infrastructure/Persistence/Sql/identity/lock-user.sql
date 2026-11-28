-- Row-locks one live user for an If-Match update and returns its current xmin (the ETag); empty when unknown or deleted. Caller: UserAdmin.
SELECT xmin::text::bigint AS "Value" FROM users WHERE id = @id AND NOT is_deleted FOR UPDATE
