import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { injectInfiniteQuery, injectMutation, keepPreviousData, QueryClient } from '@tanstack/angular-query';
import { CustomersApi, customerKeys } from '../../api/customers.api';
import { CustomerDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { CorrelationService } from '../../core/http/correlation.service';
import { ApiProblem, isApiProblem } from '../../core/http/problem';
import { PrefsStore } from '../../core/ui/prefs.store';
import { ToastStore } from '../../core/ui/toast.store';
import { MaskEmailPipe, RelativeTimePipe, StampPipe } from '../../ui/format';
import { Overlays } from '../../ui/overlays';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { CustomerDrawer, CustomerDrawerData } from './customer-drawer';
import { CustomersFilterStore } from './customers-filter.store';

@Component({
  selector: 'fg-customers-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HasPermissionDirective,
    MaskEmailPipe,
    RelativeTimePipe,
    StampPipe,
    SkeletonRows,
    EmptyState,
    ErrorState,
    CdkMenu,
    CdkMenuItem,
    CdkMenuTrigger,
  ],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.css',
})
export class CustomersPage {
  private readonly api = inject(CustomersApi);
  private readonly overlays = inject(Overlays);
  private readonly toasts = inject(ToastStore);
  private readonly queryClient = inject(QueryClient);
  private readonly correlation = inject(CorrelationService);
  protected readonly access = inject(AccessService);
  protected readonly filters = inject(CustomersFilterStore);
  protected readonly prefs = inject(PrefsStore);

  protected readonly searchText = signal(this.filters.search());
  protected readonly seesAll = computed(() => this.access.has('CUSTOMERS_ALL'));

  protected readonly query = injectInfiniteQuery(() => ({
    queryKey: customerKeys.list({ search: this.filters.search(), status: this.filters.status() }),
    queryFn: ({ pageParam }) =>
      this.api.list({ search: this.filters.search(), status: this.filters.status() }, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
  }));

  protected readonly rows = computed(() => this.query.data()?.pages.flatMap((p) => p.items) ?? []);
  protected readonly filtered = computed(() => !!this.filters.search() || !!this.filters.status());

  protected readonly transition = injectMutation(() => ({
    mutationFn: (v: { c: CustomerDto; to: 'Active' | 'Suspended' }) => this.api.transition(v.c.id, v.to),
    meta: { handles: [409] },
    onSuccess: (_d, v) =>
      this.toasts.ok(
        v.to === 'Suspended' ? `${v.c.name} suspended` : `${v.c.name} reactivated`,
        undefined,
        this.correlation.id(),
      ),
    onError: (e) => this.explainConflict(e),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: customerKeys.all }),
  }));

  protected readonly remove = injectMutation(() => ({
    mutationFn: (c: CustomerDto) => this.api.remove(c.id),
    onSuccess: (_d, c) => this.toasts.ok(`${c.name} deleted`, undefined, this.correlation.id()),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: customerKeys.all }),
  }));

  constructor() {
    // Debounce typing into the filter store; the query key follows the store.
    effect((onCleanup) => {
      const text = this.searchText();
      const t = setTimeout(() => untracked(() => this.filters.setSearch(text)), 280);
      onCleanup(() => clearTimeout(t));
    });
  }

  protected open(c?: CustomerDto): void {
    this.overlays.drawer<CustomerDrawer, CustomerDrawerData, unknown>(CustomerDrawer, {
      id: c?.id ?? null,
      name: c?.name,
    });
  }

  protected async confirmDelete(c: CustomerDto): Promise<void> {
    const ok = await this.overlays.confirm({
      title: `Delete ${c.name}?`,
      body: 'The customer disappears from every list. The audit log keeps the record of the change.',
      confirmLabel: 'Delete customer',
    });
    if (ok) this.remove.mutate(c);
  }

  protected clearFilters(): void {
    this.searchText.set('');
    this.filters.reset();
  }

  protected ownerLabel(c: CustomerDto): string {
    if (!c.ownerId) return 'Unassigned';
    if (c.ownerId === this.access.me()?.userId) return 'You';
    return '…' + c.ownerId.slice(-6);
  }

  private explainConflict(e: unknown): void {
    if (!isApiProblem(e) || e.status !== 409) return;
    this.toasts.show({
      tone: 'warn',
      status: 409,
      title: 'Status already changed',
      body: 'Someone else changed this customer first. The list now shows the current status.',
      code: (e as ApiProblem).code,
      ref: e.correlationId,
    });
  }
}
