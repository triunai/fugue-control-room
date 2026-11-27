import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  injectInfiniteQuery,
  injectMutation,
  injectQuery,
  keepPreviousData,
  QueryClient,
} from '@tanstack/angular-query';
import { AccessApi, accessKeys, PERMISSION_CODES, SetOverride } from '../../api/access.api';
import type { UserDto } from '../../api/generated/model';
import { OverrideDto } from '../../api/models';
import { AccessService } from '../../core/auth/access.service';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { CorrelationService } from '../../core/http/correlation.service';
import { ToastStore } from '../../core/ui/toast.store';
import { StampPipe } from '../../ui/format';
import { RefChip } from '../../ui/ref-chip';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { RolesMatrix } from './roles-matrix';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

@Component({
  selector: 'fg-access-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    HasPermissionDirective,
    RefChip,
    StampPipe,
    SkeletonRows,
    EmptyState,
    ErrorState,
    RolesMatrix,
  ],
  templateUrl: './access-page.html',
  styleUrl: './access-page.css',
})
export class AccessPage {
  private readonly api = inject(AccessApi);
  private readonly queryClient = inject(QueryClient);
  private readonly toasts = inject(ToastStore);
  private readonly correlation = inject(CorrelationService);
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly access = inject(AccessService);

  protected readonly codes = PERMISSION_CODES;
  protected readonly userIdText = signal('');
  protected readonly userId = signal<string | null>(null);
  protected readonly userLabel = signal<string | null>(null);

  protected readonly groups = injectQuery(() => ({
    queryKey: accessKeys.groups,
    queryFn: () => this.api.groups(),
    enabled: this.access.has('GROUPS_VIEW'),
  }));

  /** The search box text, and the debounced value the query uses. */
  protected readonly userSearch = signal('');
  protected readonly userQ = signal('');

  protected readonly users = injectInfiniteQuery(() => ({
    queryKey: accessKeys.users(this.userQ()),
    queryFn: ({ pageParam }) => this.api.users(this.userQ(), pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    placeholderData: keepPreviousData,
    enabled: this.access.has('USERS_VIEW'),
  }));
  protected readonly userRows = computed(() => this.users.data()?.pages.flatMap((p) => p.items) ?? []);

  constructor() {
    effect((onCleanup) => {
      const text = this.userSearch().trim();
      const timer = setTimeout(() => untracked(() => this.userQ.set(text)), 300);
      onCleanup(() => clearTimeout(timer));
    });
  }

  protected readonly overrides = injectQuery(() => ({
    queryKey: accessKeys.overrides(this.userId() ?? ''),
    queryFn: () => this.api.overrides(this.userId()!),
    enabled: !!this.userId() && this.access.has('OVERRIDES_VIEW'),
  }));
  protected readonly isSelf = computed(() => this.userId() === this.access.me()?.userId);
  /** Deny beats allow beats role: an unexpired deny on a code wins over everything else. */
  protected readonly winningDenies = computed(
    () => new Set((this.overrides.data() ?? []).filter((o) => !o.isAllowed && !o.expired).map((o) => o.permission)),
  );

  protected readonly form = this.fb.group({
    permission: ['CUSTOMERS_ALL' as string, Validators.required],
    effect: ['deny' as 'allow' | 'deny'],
    reason: ['', Validators.maxLength(200)],
    expiresAt: [''],
  });

  protected readonly save = injectMutation(() => ({
    mutationFn: (v: { permission: string; body: SetOverride; exists: boolean }) =>
      this.api.set(this.userId()!, v.permission, v.body, v.exists),
    onSuccess: (_d, v) =>
      this.toasts.ok(
        `${v.body.isAllowed ? 'Allow' : 'Deny'} ${v.permission} saved`,
        'It applies on the user’s next request.',
        this.correlation.id(),
      ),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: ['access', 'overrides'] }),
  }));

  protected readonly remove = injectMutation(() => ({
    mutationFn: (o: OverrideDto) => this.api.remove(o.userId, o.permission),
    onSuccess: (_d, o) => this.toasts.ok(`Override on ${o.permission} removed`, undefined, this.correlation.id()),
    onSettled: () => this.queryClient.invalidateQueries({ queryKey: ['access', 'overrides'] }),
  }));

  protected pickUser(u: UserDto): void {
    this.pick(u.id, u.displayName || u.email);
  }

  protected pick(id: string, name: string | null): void {
    this.userIdText.set(id);
    this.userId.set(id);
    this.userLabel.set(name);
  }

  protected lookUp(): void {
    const id = this.userIdText().trim();
    if (!UUID.test(id)) {
      this.toasts.show({
        tone: 'warn',
        title: 'That is not a user id',
        body: 'Paste the full id, for example from the audit log.',
      });
      return;
    }
    this.pick(id, null);
  }

  protected submit(): void {
    const v = this.form.getRawValue();
    const exists = (this.overrides.data() ?? []).some((o) => o.permission === v.permission);
    this.save.mutate({
      permission: v.permission,
      exists,
      body: {
        isAllowed: v.effect === 'allow',
        reason: v.reason.trim() || null,
        expiresAt: v.expiresAt ? new Date(v.expiresAt).toISOString() : null,
      },
    });
  }
}
