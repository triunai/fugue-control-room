-- Per-officer workload board (percentile_cont needs raw SQL). Starts from every active user who effectively holds CLAIMS_HANDLE (direct roles, group roles, allow/deny overrides; module switches not applied) plus anyone still assigned a claim, then left joins claims so idle officers show zeros. Caller: ClaimReports.
WITH handlers AS (
  SELECT u.id FROM users u
  WHERE u.is_active AND NOT u.is_deleted
    AND EXISTS (
      SELECT 1 FROM permissions p
      WHERE p.code = 'CLAIMS_HANDLE'
        AND (
          p.id IN (
            SELECT rp.permission_id FROM role_permissions rp JOIN roles r ON r.id = rp.role_id AND r.is_active
            WHERE rp.role_id IN (
              SELECT ur.role_id FROM user_roles ur WHERE ur.user_id = u.id
              UNION
              SELECT gr.role_id FROM group_members gm
                JOIN groups g ON g.id = gm.group_id AND g.is_active
                JOIN group_roles gr ON gr.group_id = g.id
              WHERE gm.user_id = u.id))
          OR EXISTS (SELECT 1 FROM user_permission_overrides o WHERE o.user_id = u.id AND o.permission_id = p.id AND o.is_allowed AND (o.expires_at IS NULL OR o.expires_at > @asOf)))
        AND NOT EXISTS (SELECT 1 FROM user_permission_overrides o WHERE o.user_id = u.id AND o.permission_id = p.id AND NOT o.is_allowed AND (o.expires_at IS NULL OR o.expires_at > @asOf)))
),
officers AS (
  SELECT id FROM handlers
  UNION
  SELECT assigned_officer_id FROM claims WHERE assigned_officer_id IS NOT NULL
)
SELECT o.id AS officer_id,
       u.display_name AS officer_name,
       count(c.id) FILTER (WHERE c.status = 'UnderReview')::int AS under_review,
       count(c.id) FILTER (WHERE c.status = 'InfoRequested')::int AS info_requested,
       min(c.submitted_at) FILTER (WHERE c.status IN ('UnderReview', 'InfoRequested')) AS oldest_open_submitted_at,
       (array_agg(c.id ORDER BY c.submitted_at, c.id) FILTER (WHERE c.status IN ('UnderReview', 'InfoRequested')))[1] AS oldest_open_claim_id,
       count(c.id) FILTER (WHERE c.decided_at >= @asOf - interval '7 days')::int AS decided_week,
       count(c.id) FILTER (WHERE c.decided_at >= @asOf - interval '30 days')::int AS decided_month,
       (percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM (c.decided_at - c.submitted_at)) / 86400.0)
           FILTER (WHERE c.decided_at >= @asOf - interval '30 days'))::float8 AS median_days
FROM officers o
JOIN users u ON u.id = o.id
LEFT JOIN claims c ON c.assigned_officer_id = o.id
GROUP BY o.id, u.display_name
ORDER BY u.display_name, o.id
