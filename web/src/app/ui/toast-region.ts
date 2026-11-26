import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastStore } from '../core/ui/toast.store';
import { RefChip } from './ref-chip';

@Component({
  selector: 'fg-toast-region',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RefChip],
  template: `
    <div class="region" aria-live="polite" aria-relevant="additions">
      @for (t of toasts.toasts(); track t.id) {
        <div class="toast" [attr.data-tone]="t.tone" [attr.role]="t.tone === 'err' ? 'alert' : 'status'">
          <div class="flex items-start gap-3">
            <div class="min-w-0 flex-1">
              <p class="title">
                @if (t.status) {
                  <span class="status">{{ t.status }}</span>
                }
                {{ t.title }}
                @if (t.developer) {
                  <span class="pill pill-warn pill-plain ml-1">client bug</span>
                }
              </p>
              @if (t.body) {
                <p class="body">{{ t.body }}</p>
              }
              @if (t.ref || t.code) {
                <div class="mt-2 flex flex-wrap items-center gap-2">
                  @if (t.ref) {
                    <fg-ref-chip label="ref" [value]="t.ref" />
                  }
                  @if (t.code) {
                    <span class="mono text-muted">{{ t.code }}</span>
                  }
                </div>
              }
            </div>
            <button
              type="button"
              class="btn btn-quiet btn-sm btn-icon -mr-2 -mt-1"
              aria-label="Dismiss"
              (click)="toasts.dismiss(t.id)"
            >
              <svg width="14" height="14" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
              </svg>
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .region {
      position: fixed;
      right: 1.25rem;
      bottom: 1.25rem;
      z-index: 1100;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      width: min(26rem, calc(100vw - 2.5rem));
    }
    .toast {
      background: var(--surface2);
      border: 1px solid var(--line);
      border-left: 4px solid var(--accent);
      border-radius: var(--radius-panel);
      padding: 0.8rem 0.9rem 0.85rem 1rem;
      box-shadow: var(--shadow-overlay);
      animation: toast-in 180ms ease-out;
    }
    .toast[data-tone='err'] {
      border-left-color: var(--err);
    }
    .toast[data-tone='warn'] {
      border-left-color: var(--warn);
    }
    .toast[data-tone='ok'] {
      border-left-color: var(--ok);
    }
    .title {
      margin: 0;
      font-weight: 600;
      line-height: 1.35;
    }
    .status {
      font-family: var(--font-mono);
      font-weight: 500;
      margin-right: 0.35rem;
    }
    .toast[data-tone='err'] .status {
      color: var(--err-ink);
    }
    .toast[data-tone='warn'] .status {
      color: var(--warn-ink);
    }
    .body {
      margin: 0.2rem 0 0;
      color: var(--muted);
      font-size: 0.9333rem;
    }
    @keyframes toast-in {
      from {
        opacity: 0;
        transform: translateY(6px);
      }
      to {
        opacity: 1;
        transform: none;
      }
    }
  `,
})
export class ToastRegion {
  protected readonly toasts = inject(ToastStore);
}
