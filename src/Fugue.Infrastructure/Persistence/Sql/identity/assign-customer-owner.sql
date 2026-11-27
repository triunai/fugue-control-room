-- Dev seed: gives an unowned seeded customer an owner. Caller: IdentitySeeder.
UPDATE customers SET user_id = @UserId, updated_at = @Now, updated_by = @Actor WHERE id = @Id AND user_id IS NULL
