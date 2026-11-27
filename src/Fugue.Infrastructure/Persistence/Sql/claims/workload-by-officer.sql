-- Per-officer workload board (percentile_cont needs raw SQL). Caller: ClaimReports.
SELECT c.assigned_officer_id AS officer_id,
       u.display_name AS officer_name,
       count(*) FILTER (WHERE c.status = 'UnderReview')::int AS under_review,
       count(*) FILTER (WHERE c.status = 'InfoRequested')::int AS info_requested,
       min(c.submitted_at) FILTER (WHERE c.status IN ('UnderReview', 'InfoRequested')) AS oldest_open_submitted_at,
       count(*) FILTER (WHERE c.decided_at >= @asOf - interval '7 days')::int AS decided_week,
       count(*) FILTER (WHERE c.decided_at >= @asOf - interval '30 days')::int AS decided_month,
       (percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM (c.decided_at - c.submitted_at)) / 86400.0)
           FILTER (WHERE c.decided_at >= @asOf - interval '30 days'))::float8 AS median_days
FROM claims c
JOIN users u ON u.id = c.assigned_officer_id
GROUP BY c.assigned_officer_id, u.display_name
ORDER BY u.display_name, c.assigned_officer_id
