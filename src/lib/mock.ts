export type CustomerStatus = "active" | "suspended" | "pending";
export type Customer = {
  id: string; name: string; email: string; plan: "free" | "team" | "enterprise";
  status: CustomerStatus; createdAt: string; version: number;
};
const first = ["Ada","Basil","Cora","Dorian","Elsa","Faust","Greta","Hugo","Ilse","Jonas","Klara","Lucan","Mira","Nils","Odile","Pavel","Quinn","Rhea","Silas","Thea"];
const last = ["Vane","Morrow","Ashgrove","Blackwood","Thorne","Crane","Hollow","Grey","Wren","Marsh"];
export const customers: Customer[] = Array.from({ length: 58 }, (_, i) => {
  const f = first[i % first.length]!; const l = last[(i * 7) % last.length]!;
  const statuses: CustomerStatus[] = ["active","active","active","suspended","pending"];
  return {
    id: `cus_${String(i + 1).padStart(4, "0")}`, name: `${f} ${l}`,
    email: `${f.toLowerCase()}.${l.toLowerCase()}@example.com`,
    plan: (["free","team","enterprise"] as const)[i % 3]!,
    status: statuses[i % statuses.length]!,
    createdAt: new Date(Date.UTC(2026, 0, 1) + i * 86400000 * 5).toISOString(), version: 1,
  };
});

export const roles = [
  { id: "r_admin", name: "Administrator", perms: ["customers:*", "users:*", "modules:*", "audit:read", "webhooks:*"] },
  { id: "r_support", name: "Support", perms: ["customers:read", "customers:write", "audit:read"] },
  { id: "r_billing", name: "Billing", perms: ["customers:read", "invoices:*"] },
  { id: "r_dev", name: "Developer", perms: ["webhooks:*", "modules:read", "audit:read"] },
];
export const groups = [
  { id: "g_ops", name: "Operations", members: 6 },
  { id: "g_emea", name: "EMEA Support", members: 11 },
  { id: "g_platform", name: "Platform", members: 4 },
];
export type Override = { perm: string; effect: "allow" | "deny"; expires: string | null; reason: string };
export type StaffUser = {
  id: string; name: string; email: string; staff: boolean; lastSeen: string;
  roles: string[]; groups: string[]; overrides: Override[];
};
export const users: StaffUser[] = [
  { id: "usr_01", name: "Morgan Vale", email: "admin@fugue.dev", staff: true, lastSeen: "2 min ago", roles: ["r_admin"], groups: ["g_platform"], overrides: [] },
  { id: "usr_02", name: "Iris Calloway", email: "iris@fugue.dev", staff: true, lastSeen: "1 h ago", roles: ["r_support","r_billing"], groups: ["g_emea"], overrides: [
    { perm: "customers:delete", effect: "allow", expires: "2026-12-15", reason: "Data cleanup ticket OPS-412" },
    { perm: "customers:delete", effect: "deny", expires: null, reason: "Compliance hold on EMEA accounts" },
    { perm: "audit:export", effect: "allow", expires: "2026-12-01", reason: "Quarterly review" },
    { perm: "invoices:refund", effect: "deny", expires: "2027-01-31", reason: "Pending training" },
  ] },
  { id: "usr_03", name: "Tobias Wren", email: "tobias@fugue.dev", staff: true, lastSeen: "yesterday", roles: ["r_dev"], groups: ["g_platform","g_ops"], overrides: [
    { perm: "modules:write", effect: "allow", expires: "2026-11-30", reason: "Rollout of Billing v2" },
  ] },
  { id: "usr_04", name: "Selma Ashgrove", email: "selma@fugue.dev", staff: true, lastSeen: "3 days ago", roles: ["r_support"], groups: ["g_emea"], overrides: [] },
  { id: "usr_05", name: "June Marsh", email: "member@fugue.dev", staff: false, lastSeen: "5 min ago", roles: [], groups: [], overrides: [] },
];

export type Module = { key: string; name: string; desc: string; config: boolean; db: boolean; requires?: string; configNote?: string };
export const modules: Module[] = [
  { key: "customers", name: "Customers", desc: "Customer records, plans and lifecycle.", config: true, db: true },
  { key: "billing", name: "Billing", desc: "Invoices, payment methods and refunds.", config: false, db: true, configNote: "FUGUE_MODULE_BILLING=false in production.env" },
  { key: "webhooks", name: "Webhooks", desc: "Outbound event subscriptions and deliveries.", config: true, db: true },
  { key: "reports", name: "Reports", desc: "Scheduled exports and usage reports.", config: true, db: false },
  { key: "invoicing", name: "Invoicing", desc: "PDF invoice generation.", config: true, db: true, requires: "billing" },
  { key: "sso", name: "Single sign-on", desc: "SAML and OIDC identity providers.", config: true, db: true },
];

export type AuditEntry = {
  id: string; at: string; actor: string; action: "create" | "update" | "delete" | "suspend" | "login";
  entity: string; entityId: string; correlationId: string;
  diff: { field: string; before: string | null; after: string | null; masked?: boolean }[];
};
export const audit: AuditEntry[] = [
  { id: "aud_9", at: "2026-11-26T03:12:04Z", actor: "iris@fugue.dev", action: "update", entity: "customer", entityId: "cus_0007", correlationId: "c7f1-2a9e-4b10",
    diff: [{ field: "plan", before: "team", after: "enterprise" }, { field: "email", before: "greta.hollow@example.com", after: "greta@hollow.io" }, { field: "api_key", before: "x", after: "y", masked: true }] },
  { id: "aud_8", at: "2026-11-26T02:51:40Z", actor: "admin@fugue.dev", action: "suspend", entity: "customer", entityId: "cus_0004", correlationId: "9b02-ee41-7c3d",
    diff: [{ field: "status", before: "active", after: "suspended" }] },
  { id: "aud_7", at: "2026-11-26T01:20:11Z", actor: "tobias@fugue.dev", action: "create", entity: "webhook", entityId: "wh_03", correlationId: "11aa-04f2-d88e",
    diff: [{ field: "url", before: null, after: "https://hooks.thorne.dev/fugue" }, { field: "secret", before: null, after: "s", masked: true }] },
  { id: "aud_6", at: "2026-11-25T22:03:58Z", actor: "admin@fugue.dev", action: "update", entity: "module", entityId: "reports", correlationId: "52c9-7a10-0f6b",
    diff: [{ field: "enabled", before: "true", after: "false" }] },
  { id: "aud_5", at: "2026-11-25T18:44:00Z", actor: "selma@fugue.dev", action: "delete", entity: "customer", entityId: "cus_0061", correlationId: "a3e0-61bd-2290",
    diff: [{ field: "name", before: "Pavel Crane", after: null }, { field: "email", before: "pavel.crane@example.com", after: null }] },
  { id: "aud_4", at: "2026-11-25T09:15:22Z", actor: "admin@fugue.dev", action: "update", entity: "user", entityId: "usr_02", correlationId: "e410-9c3a-55d1",
    diff: [{ field: "override.customers:delete", before: null, after: "deny" }] },
  { id: "aud_3", at: "2026-11-24T16:30:09Z", actor: "member@fugue.dev", action: "login", entity: "session", entityId: "ses_88f", correlationId: "0d7b-1e22-a9c4", diff: [] },
];

export type Webhook = { id: string; url: string; events: string[]; active: boolean; secretHint: string; createdAt: string };
export const webhooks: Webhook[] = [
  { id: "wh_01", url: "https://api.blackwood.io/hooks/fugue", events: ["customer.created", "customer.updated"], active: true, secretHint: "whsec_••••a91f", createdAt: "2026-09-02" },
  { id: "wh_02", url: "https://ops.morrow.net/ingest", events: ["customer.deleted"], active: false, secretHint: "whsec_••••0c3e", createdAt: "2026-10-18" },
  { id: "wh_03", url: "https://hooks.thorne.dev/fugue", events: ["*"], active: true, secretHint: "whsec_••••77b2", createdAt: "2026-11-26" },
];
export type Delivery = { id: string; webhookId: string; event: string; status: "delivered" | "failed" | "retrying" | "pending"; attempts: number; nextRetry: string | null; code: number | null; at: string };
export const deliveries: Delivery[] = [
  { id: "dlv_120", webhookId: "wh_01", event: "customer.updated", status: "delivered", attempts: 1, nextRetry: null, code: 200, at: "03:12:05" },
  { id: "dlv_119", webhookId: "wh_03", event: "customer.updated", status: "retrying", attempts: 3, nextRetry: "03:42 UTC", code: 503, at: "03:12:05" },
  { id: "dlv_118", webhookId: "wh_01", event: "customer.created", status: "delivered", attempts: 2, nextRetry: null, code: 204, at: "02:40:17" },
  { id: "dlv_117", webhookId: "wh_02", event: "customer.deleted", status: "failed", attempts: 8, nextRetry: null, code: 410, at: "yesterday" },
  { id: "dlv_116", webhookId: "wh_03", event: "customer.suspended", status: "failed", attempts: 8, nextRetry: null, code: null, at: "yesterday" },
];

export const correlationId = () =>
  Array.from({ length: 3 }, () => Math.random().toString(16).slice(2, 6)).join("-");
export const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));
export class ApiError extends Error {
  constructor(public status: number, message: string, public correlationId = correlationIdFn(), public fields?: Record<string, string>) { super(message); }
}
function correlationIdFn() { return correlationId(); }
