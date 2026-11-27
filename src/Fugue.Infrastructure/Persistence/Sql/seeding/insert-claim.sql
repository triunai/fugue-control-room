-- Inserts one seeded claim; ON CONFLICT (number) makes a second run a no-op. Caller: ClaimSeeder.
INSERT INTO claims (id, number, market, currency, line, status, claimant_user_id, assigned_officer_id, assigned_at, policy_number, incident_date,
                    incident_description, incident_location, submitted_at, reserve_minor, approved_amount_minor, decision_reason, decided_at, settled_at,
                    created_at, created_by, updated_at, updated_by)
VALUES (@Id, @Number, @Market, @Currency, @Line, @Status, @ClaimantUserId, @AssignedOfficerId, @AssignedAt, @PolicyNumber, @IncidentDate,
        @IncidentDescription, @IncidentLocation, @SubmittedAt, @ReserveMinor, @ApprovedAmountMinor, @DecisionReason, @DecidedAt, @SettledAt,
        @CreatedAt, @Actor, @UpdatedAt, @Actor)
ON CONFLICT (number) DO NOTHING
