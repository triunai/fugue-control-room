-- Transaction-scoped advisory lock keyed by the token family (serializes rotation with revocation). Caller: RefreshTokenStore.
SELECT pg_advisory_xact_lock(hashtextextended(@family, 0))
