import { Dialog, DialogConfig, DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';
import { ComponentType } from '@angular/cdk/portal';
import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';

/** Opens components as a right-hand drawer or a centred dialog (CDK Dialog: focus trap, Escape, aria-modal). */
@Injectable({ providedIn: 'root' })
export class Overlays {
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);

  drawer<C, D, R>(
    component: ComponentType<C>,
    data: D,
    config: DialogConfig<D, DialogRef<R, C>> = {},
  ): DialogRef<R, C> {
    return this.dialog.open<R, D, C>(component, {
      data,
      width: 'min(520px, 100vw)',
      height: '100vh',
      panelClass: 'fg-drawer',
      backdropClass: 'fg-scrim',
      positionStrategy: this.overlay.position().global().right('0').top('0'),
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      ...config,
    });
  }

  modal<C, D, R>(component: ComponentType<C>, data: D, config: DialogConfig<D, DialogRef<R, C>> = {}): DialogRef<R, C> {
    return this.dialog.open<R, D, C>(component, {
      data,
      width: 'min(560px, calc(100vw - 2rem))',
      panelClass: 'fg-modal',
      backdropClass: 'fg-scrim',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      ...config,
    });
  }

  confirm(data: ConfirmData): Promise<boolean> {
    const ref = this.modal<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, data, {
      width: 'min(460px, calc(100vw - 2rem))',
      role: 'alertdialog',
      autoFocus: 'dialog',
    });
    return new Promise((resolve) => ref.closed.subscribe((v) => resolve(v === true)));
  }
}

export interface ConfirmData {
  title: string;
  body: string;
  confirmLabel: string;
  tone?: 'danger' | 'primary';
}

@Component({
  selector: 'fg-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6">
      <h2 class="section-title">{{ data.title }}</h2>
      <p class="mt-2 mb-0 text-muted">{{ data.body }}</p>
      <div class="mt-6 flex justify-end gap-2">
        <button type="button" class="btn" (click)="ref.close(false)">Cancel</button>
        <button
          type="button"
          class="btn"
          [class.btn-danger-solid]="data.tone !== 'primary'"
          [class.btn-primary]="data.tone === 'primary'"
          (click)="ref.close(true)"
        >
          {{ data.confirmLabel }}
        </button>
      </div>
    </div>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
}
