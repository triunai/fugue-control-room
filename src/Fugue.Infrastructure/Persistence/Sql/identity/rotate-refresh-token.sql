-- Rotation: claims the old refresh token with ONE conditional UPDATE (unrevoked, unexpired, active user and tenant). Caller: RefreshTokenStore.
UPDATE refresh_tokens SET revoked_at = @now, replaced_by_id = @newId, updated_at = @stampNow, updated_by = @actor
WHERE id = @id AND revoked_at IS NULL AND expires_at > @now
  AND EXISTS (SELECT 1 FROM users u WHERE u.id = refresh_tokens.user_id AND u.is_active AND NOT u.is_deleted
              AND (u.tenant_id IS NULL OR EXISTS (SELECT 1 FROM tenants t WHERE t.id = u.tenant_id AND t.is_active)))
