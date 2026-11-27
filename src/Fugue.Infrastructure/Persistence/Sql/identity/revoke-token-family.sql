-- Revokes every live token of a family (theft response, logout). Caller: RefreshTokenStore.
UPDATE refresh_tokens SET revoked_at = @now, updated_at = @stampNow, updated_by = @actor WHERE family_id = @family AND revoked_at IS NULL
