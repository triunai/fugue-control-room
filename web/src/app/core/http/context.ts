import { HttpContextToken } from '@angular/common/http';

/** Set to a fixed id to pin a request's X-Correlation-Id (background polls, replays). */
export const CORRELATION_ID = new HttpContextToken<string | null>(() => null);
/** false: do not attach If-Match from the ETag store (create-only calls, deliberate unconditional writes). */
export const SEND_IF_MATCH = new HttpContextToken<boolean>(() => true);
/** false: do not record the response ETag (a "peek" at the latest version that must not replace the tag you edit against). */
export const TRACK_ETAG = new HttpContextToken<boolean>(() => true);
/** Internal: the request is a replay after a refresh; never refresh twice. */
export const AUTH_RETRIED = new HttpContextToken<boolean>(() => false);
