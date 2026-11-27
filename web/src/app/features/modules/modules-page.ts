import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { injectMutation, injectQuery, QueryClient } from '@tanstack/angular-query';
import { ModuleDto } from '../../api/models';
import { moduleKeys, ModulesApi } from '../../api/modules.api';
import { AccessService, ME_QUERY_KEY } from '../../core/auth/access.service';
import { CorrelationService } from '../../core/http/correlation.service';
import { isApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { Overlays } from '../../ui/overlays';
import { ErrorState } from '../../ui/states';
import { Switch } from '../../ui/switch';
import { LastChanged } from './last-changed';
import { whenOff } from './when-off';

const PROTECTED = 'SYSTEM';

@Component({
  selector: 'fg-modules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErrorState, Switch, LastChanged],
  templateUrl: './modules-page.html',
  styleUrl: './modules-page.css',
})
export class ModulesPage {
  private readonly api = inject(ModulesApi);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly overlays = inject(Overlays);
  private readonly correlation = inject(CorrelationService);
  protected readonly access = inject(AccessService);

  protected readonly modules = injectQuery(() => ({ queryKey: moduleKeys.all, queryFn: () => this.api.list() }));

  protected readonly toggle = injectMutation(() => ({
    mutationFn: (v: { m: ModuleDto; on: boolean }) => this.api.setActive(v.m.code, v.on),
    meta: { handles: [409, 412] },
    onSuccess: (_d, v) =>
      this.toasts.ok(
        `${v.m.name} switched ${v.on ? 'on' : 'off'}`,
        v.on ? undefined : 'Its pages and permissions are gone for everyone until it is switched back on.',
        this.correlation.id(),
      ),
    onError: (e) => {
      if (isApiProblem(e) && e.status === 412) {
        this.toasts.show({
          tone: 'warn',
          status: 412,
          title: 'Changed since you loaded it',
          body: 'Someone else switched this module first. The table now shows the current state.',
          code: e.code,
          ref: e.correlationId,
        });
      } else if (isApiProblem(e) && e.status === 409) {
        this.toasts.show({ tone: 'warn', status: 409, title: e.title, code: e.code, ref: e.correlationId });
      }
    },
    // /me changes too: a module's permissions leave everyone's effective set, so the navigation re-renders.
    onSettled: () =>
      Promise.all([
        this.queryClient.invalidateQueries({ queryKey: moduleKeys.all }),
        this.queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
      ]),
  }));

  protected locked(m: ModuleDto): boolean {
    return m.code === PROTECTED;
  }

  /** The AUDIT module's effective state; while it is off the Last changed column must not ask the audit endpoint. */
  protected readonly auditOn = computed(() => (this.modules.data() ?? []).find((m) => m.code === 'AUDIT')?.effective ?? true);

  protected readonly onCount = computed(() => (this.modules.data() ?? []).filter((m) => m.effective).length);

  protected whenOff = whenOff;

  protected disabledReason(m: ModuleDto): string | null {
    if (this.locked(m)) return null;
    if (!m.configEnabled)
      return `Off in configuration (Modules__${m.code}__Enabled); the switch cannot override it. An operator must change the setting.`;
    if (!this.access.has('MODULES_MANAGE')) return 'You need MODULES_MANAGE to switch modules';
    return null;
  }

  protected async flip(m: ModuleDto): Promise<void> {
    const on = !m.dbActive;
    if (!on) {
      const ok = await this.overlays.confirm({
        title: `Switch off ${m.name}?`,
        body: 'Its pages disappear from the navigation and its endpoints answer Not found for everyone, you included, until it is switched back on.',
        confirmLabel: `Switch off ${m.name}`,
      });
      if (!ok) return;
    }
    this.toggle.mutate({ m, on });
  }
}
