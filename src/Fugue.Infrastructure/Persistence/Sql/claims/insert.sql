-- Inserts a submitted claim. Caller: ClaimRepository.
INSERT INTO claims (id, number, market, currency, line, status, claimant_user_id, assigned_officer_id, assigned_at, policy_number, incident_date,
                    incident_description, incident_location, submitted_at, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @Number, @Market, @Currency, @Line, @Status, @ClaimantUserId, @AssignedOfficerId, @AssignedAt, @PolicyNumber, @IncidentDate,
        @IncidentDescription, @IncidentLocation, @SubmittedAt, @Now, @Actor, @Now, @Actor)
