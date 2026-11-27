-- Keyset batch of claims after the search indexer checkpoint (updated_at, id), oldest change first; EF composes the ordering and the limit. Caller: ClaimIndexSource.
SELECT id, number, market, currency, line, status, claimant_user_id, assigned_officer_id, assigned_at, policy_number, incident_date,
incident_description, incident_location, submitted_at, reserve_minor, approved_amount_minor, decision_reason, decided_at, settled_at,
created_at, created_by, updated_at, updated_by, xmin
FROM claims WHERE (updated_at, id) > (@updatedAt, @id)
