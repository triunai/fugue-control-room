-- Effective permissions of one user in ONE statement (roles, groups, allow/deny overrides, module switches). Caller: UserAccessReader.
WITH u AS (
  SELECT u0.id, u0.tenant_id FROM users u0
    WHERE u0.id = @userId AND u0.is_active AND NOT u0.is_deleted
      AND (u0.tenant_id IS NULL OR EXISTS (SELECT 1 FROM tenants t WHERE t.id = u0.tenant_id AND t.is_active))
),
role_ids AS (
  SELECT ur.role_id FROM user_roles ur JOIN u ON u.id = ur.user_id
  UNION
  SELECT gr.role_id FROM group_members gm
    JOIN u ON u.id = gm.user_id
    JOIN groups g ON g.id = gm.group_id AND g.is_active
    JOIN group_roles gr ON gr.group_id = g.id
),
granted AS (
  SELECT rp.permission_id FROM role_permissions rp
    JOIN roles r ON r.id = rp.role_id AND r.is_active
    WHERE rp.role_id IN (SELECT role_id FROM role_ids)
  UNION
  SELECT o.permission_id FROM user_permission_overrides o JOIN u ON u.id = o.user_id
    WHERE o.is_allowed AND (o.expires_at IS NULL OR o.expires_at > @now)
),
denied AS (
  SELECT o.permission_id FROM user_permission_overrides o JOIN u ON u.id = o.user_id
    WHERE NOT o.is_allowed AND (o.expires_at IS NULL OR o.expires_at > @now)
)
SELECT 'u' AS "Kind", COALESCE(u.tenant_id::text, '') AS "Code" FROM u
UNION ALL
SELECT 'p', p.code
  FROM permissions p
  JOIN resources rs ON rs.id = p.resource_id AND NOT rs.is_deleted
  JOIN modules m ON m.id = rs.module_id AND NOT m.is_deleted AND m.is_active
  JOIN actions a ON a.id = p.action_id AND NOT a.is_deleted
  WHERE EXISTS (SELECT 1 FROM u)
    AND p.id IN (SELECT permission_id FROM granted)
    AND p.id NOT IN (SELECT permission_id FROM denied)
    AND m.code <> ALL (@disabled)
