import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { ApiProblem } from '../http/problem';

export type ToastTone = 'ok' | 'err' | 'warn' | 'info';

export interface Toast {
  id: number;
  tone: ToastTone;
  title: string;
  body?: string;
  /** HTTP status shown before the title, e.g. "409". */
  status?: number;
  code?: string | null;
  /** Correlation id: rendered as a click-to-copy "ref" chip. */
  ref?: string | null;
  /** Visible to developers: shown with a "client bug" marker (428). */
  developer?: boolean;
  timeoutMs: number;
}

let nextId = 1;

/** Client UI state: the global toast queue. */
export const ToastStore = signalStore(
  { providedIn: 'root' },
  withState<{ toasts: Toast[] }>({ toasts: [] }),
  withMethods((store) => {
    const dismiss = (id: number) => patchState(store, { toasts: store.toasts().filter((t) => t.id !== id) });
    const show = (toast: Omit<Toast, 'id' | 'timeoutMs'> & { timeoutMs?: number }): number => {
      const id = nextId++;
      const timeoutMs = toast.timeoutMs ?? (toast.tone === 'err' ? 9000 : 4500);
      patchState(store, { toasts: [...store.toasts().slice(-3), { ...toast, id, timeoutMs }] });
      if (timeoutMs > 0) setTimeout(() => dismiss(id), timeoutMs);
      return id;
    };
    return {
      show,
      dismiss,
      ok: (title: string, body?: string, ref?: string | null) => show({ tone: 'ok', title, body, ref }),
      info: (title: string, body?: string) => show({ tone: 'info', title, body }),
      problem(problem: ApiProblem, fallbackBody?: string): number {
        if (problem.status === 428) {
          return show({
            tone: 'warn',
            developer: true,
            status: 428,
            title: 'Sent without If-Match',
            body: 'The console wrote without a version tag. This is a client bug: reload the record and try again.',
            code: problem.code,
            ref: problem.correlationId,
            timeoutMs: 12000,
          });
        }
        return show({
          tone: problem.status >= 500 || problem.status === 0 ? 'err' : problem.status === 429 ? 'warn' : 'err',
          status: problem.status || undefined,
          title: problem.title,
          body: problem.detail ?? fallbackBody ?? bodyFor(problem),
          code: problem.code,
          ref: problem.correlationId,
        });
      },
    };
  }),
);

function bodyFor(p: ApiProblem): string | undefined {
  switch (p.status) {
    case 0:
      return 'Check that the API is running, then try again.';
    case 403:
      return 'Your role does not include this action.';
    case 404:
      return 'It may have been deleted, or it is outside what you can see.';
    case 429:
    case 503:
      return p.retryAfterSeconds != null ? `Try again in ${p.retryAfterSeconds} s.` : 'Try again shortly.';
    default:
      return p.status >= 500 ? 'Quote the ref if you report it.' : undefined;
  }
}
