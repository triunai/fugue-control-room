/**
 * Wire types for the Fugue API, generated from docs/api/openapi.v1.json by `npm run api:gen` (orval).
 * This file only re-exports them and narrows the two fields the contract leaves open (audit `changes` and
 * `metadata` are free-form JSON in the schema). Do not handwrite DTOs here: regenerate instead.
 */
import type { AuditLogDto as GeneratedAuditLogDto } from './generated/model';

export type {
  CreateCustomerRequest,
  CreateWebhookSubscriptionRequest,
  CustomerDto,
  GroupDto,
  MeDto,
  MeModuleDto,
  ModuleDto,
  OverrideDto,
  ReceivedWebhookDto,
  TokenDto,
  UpdateCustomerRequest,
  UpdateWebhookSubscriptionRequest,
  WebhookDeliveryDto,
  WebhookSubscriptionDto,
  WebhookSubscriptionSecretDto,
} from './generated/model';
export { CustomerStatus } from './generated/model';

/** `{ "field": { "old": ..., "new": ... } }`, plus `_truncated: true` when the API clipped the row (ADR-010). */
export type AuditChanges = Record<string, { old?: unknown; new?: unknown }>;

export type AuditLogDto = Omit<GeneratedAuditLogDto, 'changes' | 'metadata'> & {
  changes?: AuditChanges | null;
  metadata?: Record<string, unknown> | null;
};

/** One keyset page: the body is the array, the next cursor comes from X-Next-Cursor (or Link rel=next). */
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
}
