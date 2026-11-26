/**
 * Wire types for the Fugue API.
 * TEMPORARY: handwritten from src/Fugue.Application DTOs until docs/api/openapi.v1.json lands (W-016).
 * Once `npm run api:gen` works, these become re-exports of api/generated and this note goes.
 */
export type CustomerStatus = 'Active' | 'Suspended';

export interface CustomerDto {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  status: CustomerStatus;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  version: number;
  ownerId?: string | null;
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  phone: string | null;
  userId?: string | null;
}

export interface UpdateCustomerRequest {
  name: string;
  email: string;
  phone: string | null;
}

export interface TokenDto {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  refreshToken: string | null;
}

/** GET /api/v1/me (W-016). Permission CODES drive the UI; role names are display only. */
export interface MeDto {
  userId: string;
  email: string;
  displayName: string;
  tenantId: string | null;
  roles: string[];
  groups: string[];
  permissions: string[];
  /** Effectively enabled modules only. */
  modules: { code: string; name: string }[];
}

export interface AuditLogDto {
  id: string;
  occurredAt: string;
  actorUserId: string | null;
  actorType: string;
  actorDisplay: string | null;
  tenantId: string | null;
  permissionUsed: string | null;
  action: string;
  entityType: string | null;
  entityId: string | null;
  outcome: string;
  errorCode: string | null;
  changes: Record<string, { old?: unknown; new?: unknown }> | null;
  correlationId: string | null;
  sagaId: string | null;
  traceId: string | null;
  httpMethod: string | null;
  routeTemplate: string | null;
  clientIp: string | null;
  userAgent: string | null;
  metadata: Record<string, unknown> | null;
}

export interface ModuleDto {
  code: string;
  name: string;
  configEnabled: boolean;
  dbActive: boolean;
  effective: boolean;
  version: number;
}

export interface WebhookSubscriptionDto {
  id: string;
  name: string;
  url: string;
  eventTypes: string[];
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  version: number;
}

export interface WebhookSubscriptionSecretDto extends WebhookSubscriptionDto {
  secret: string;
}

export interface CreateWebhookSubscriptionRequest {
  name: string;
  url: string;
  eventTypes: string[];
}

export interface UpdateWebhookSubscriptionRequest extends CreateWebhookSubscriptionRequest {
  isActive: boolean;
}

export type DeliveryStatus = 'pending' | 'succeeded' | 'dead' | string;

export interface WebhookDeliveryDto {
  id: string;
  messageId: string;
  eventType: string;
  aggregateId: string;
  subscriptionId: string;
  occurredAt: string;
  status: DeliveryStatus;
  attempts: number;
  nextAttemptAt: string;
  lastStatusCode: number | null;
  lastError: string | null;
  durationMs: number | null;
  deliveredAt: string | null;
  deadLetteredAt: string | null;
}

export interface ReceivedWebhookDto {
  eventId: string;
  eventType: string;
  receivedAt: string;
  timestamp: number;
  correlationId: string | null;
  sagaId: string | null;
  traceparent: string | null;
  body: string;
}

export interface GroupDto {
  code: string;
  name: string;
  isActive: boolean;
  roles: string[];
  memberCount: number;
}

export interface OverrideDto {
  userId: string;
  permission: string;
  isAllowed: boolean;
  reason: string | null;
  expiresAt: string | null;
  expired: boolean;
  version: number;
}

/** One keyset page: the body is the array, the next cursor comes from X-Next-Cursor (or Link rel=next). */
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
}
