**Verdict: REJECT**

1. **HIGH — §3: creation bypasses ownership scope.** `OwnerScope` covers reads, updates and deletes, but staff supply arbitrary `CreateCustomerCommand.UserId`. A staff user denied `CUSTOMERS_ALL` can still create records for other owners through `CUSTOMERS_INSERT`.
   **Fix:** Require effective `CUSTOMERS_ALL` for creating another user’s or an unowned record; otherwise bind ownership to the caller. Test staff with an explicit `CUSTOMERS_ALL` deny.

2. **HIGH — §1: credential endpoint ships without abuse controls.** Rate limiting is deferred to S5 while login is enabled in every environment. Anonymous requests trigger 210,000-iteration password verification, enabling password spraying and CPU exhaustion.
   **Fix:** Include bounded login rate and concurrency limits in S3, or make equivalent ingress controls a deployment prerequisite. Test rejection before expensive verification.

3. **MED — §§2, 4: cached allows outlive override expiry.** An allow expiring just after cache population remains effective for nearly 60 seconds, contradicting “expired overrides are ignored.” Expired denies can likewise suppress legitimate access.
   **Fix:** Cap cache lifetime at the earliest applicable override expiry, using UTC and `expires_at <= now`. Add deterministic boundary tests with `TimeProvider`.

4. **MED — §2: invalidation lacks transaction and race semantics.** Only role links and overrides trigger eviction; role grants, role activation and user status also affect cached access. A concurrent stale read can repopulate an entry after eviction, extending the stated revocation bound.
   **Fix:** Specify post-commit invalidation and race-safe cache fills across all dependencies. For this horizon, an uncached reader is simpler; otherwise explicitly constrain deployment and test concurrent revocation.

5. **MED — §§3, 7: ownership filtering leaves an email enumeration oracle.** Self-service users can edit their own customer email. The existing global unique index and `customer.email_taken` response reveal whether another customer uses a guessed address, despite scoped reads.
   **Fix:** Make email changes staff-only for S3, or define a verified change workflow that does not expose cross-owner uniqueness results. Add an adversarial update test.

6. **MED — §§1, 4: email lookup and uniqueness are underspecified.** The partial unique index allows deleted-email reuse, but login does not explicitly exclude deleted rows before selecting a candidate. No canonicalization rule aligns lookup with uniqueness.
   **Fix:** Define one canonical email key, enforce active-row uniqueness on it, and query only non-deleted candidates. Test deleted-email reuse and case variants.

7. **MED — §§2, 4: deleted catalog entries can still grant access.** Modules and resources support soft deletion, but effective permissions exclude only inactive/deleted users and inactive roles. Grants through deleted modules/resources remain effective under the written algorithm.
   **Fix:** Exclude permissions beneath deleted modules/resources and invalidate affected cache entries. Test both deletion levels.

8. **MED — §§5, 9: promised HTTP contracts currently become 500s.** `MapStatus` throws, so invalid login and cross-owner failures cannot produce the promised 401/404. Result-level tests do not establish HTTP behavior, and “each green” conflicts with deliberately red assertions.
   **Fix:** Make owner completion of W-009 a release prerequisite. Require passing HTTP assertions before accepting S3; retain the prohibition on agents implementing the mapping.

9. **MED — §§7, 9: the three-day cut line leaves essential verification optional.** The plan combines ten tables, authentication, dynamic policies, ownership, overrides and tooling, while tests omit cross-owner mutations, restricted-staff creation, cache expiry/races, and cursor-page ownership filtering.
   **Fix:** Set explicit acceptance gates for those cases plus issuer/audience/algorithm rejection. Establish one complete security vertical slice before expanding administration; do not count intermediate slices as releasable.

10. **LOW — §1: HS256 key length does not establish entropy.** A predictable 32-byte password or placeholder passes the stated startup requirement.
    **Fix:** Specify generation from at least 32 cryptographically random bytes, a single encoding/decoding contract, and rejection of documented placeholders. Test malformed and undersized decoded keys.