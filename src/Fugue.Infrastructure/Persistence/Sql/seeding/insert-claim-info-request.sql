-- Inserts one seeded claim_info_requests row. Caller: ClaimSeeder.
INSERT INTO claim_info_requests (id, claim_id, question, requested_by_user_id, requested_at, response, responded_at, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @ClaimId, @Question, @RequestedByUserId, @RequestedAt, @Response, @RespondedAt, @RequestedAt, @Actor, @UpdatedAt, @Actor)
