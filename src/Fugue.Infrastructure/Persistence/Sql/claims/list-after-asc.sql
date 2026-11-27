-- Keyset page of claims after the cursor, oldest first; EF composes the filters on top. Caller: ClaimRepository.
SELECT id, number, market, currency, line, status, claimant_user_id, assigned_officer_id, assigned_at, policy_number, incident_date,
incident_description, incident_location, submitted_at, reserve_minor, approved_amount_minor, decision_reason, decided_at, settled_at,
created_at, created_by, updated_at, updated_by, xmin
FROM claims WHERE (submitted_at, id) > (@createdAt, @id)
