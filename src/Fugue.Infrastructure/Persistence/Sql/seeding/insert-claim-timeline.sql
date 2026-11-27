-- Inserts one seeded claim_timeline row. Caller: ClaimSeeder.
INSERT INTO claim_timeline (id, claim_id, occurred_at, from_status, to_status, actor_user_id, actor_display, note, created_at, created_by, updated_at, updated_by)
VALUES (@Id, @ClaimId, @OccurredAt, @FromStatus, @ToStatus, @ActorUserId, @ActorDisplay, @Note, @OccurredAt, @Actor, @OccurredAt, @Actor)
