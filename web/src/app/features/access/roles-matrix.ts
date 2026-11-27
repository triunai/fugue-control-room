import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { injectQuery } from '@tanstack/angular-query';
import { RolesApi } from '../../api/roles.api';
import { EmptyState, ErrorState, SkeletonRows } from '../../ui/states';
import { buildMatrix } from './roles-matrix.logic';

/** Read-only role x permission grid: roles as columns, permissions grouped by module as rows, a dot where granted. */
@Component({
  selector: 'fg-roles-matrix',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonRows, ErrorState, EmptyState],
  template: `
    <section class="table-wrap" aria-labelledby="rm-title" data-testid="roles-matrix">
      <h2 id="rm-title" class="section-title px-4 pt-4 pb-1">Roles and permissions</h2>
      <p class="px-4 pb-3 text-sm text-muted">
        What each role grants, by module. A user holds the union of their roles, plus whatever their groups give them.
      </p>
      @if (roles.isPending()) {
        <fg-skeleton-rows [rows]="4" />
      } @else if (roles.isError()) {
        <fg-error-state [error]="roles.error()" (retry)="roles.refetch()" />
      } @else if (matrix().groups.length === 0) {
        <fg-empty-state title="No roles" body="No role grants anything yet." />
      } @else {
        <table class="table matrix">
          <thead>
            <tr>
              <th scope="col">Permission</th>
              @for (r of matrix().roles; track r.code) {
                <th scope="col" class="cell" [title]="r.name">{{ r.code }}</th>
              }
            </tr>
          </thead>
          @for (g of matrix().groups; track g.module) {
            <tbody>
              <tr class="module-row">
                <th scope="colgroup" [attr.colspan]="matrix().roles.length + 1">
                  {{ moduleLabel(g.module) }} <span class="count">· {{ g.rows.length }}</span>
                </th>
              </tr>
              @for (row of g.rows; track row.code) {
                <tr>
                  <th scope="row" class="perm">
                    <span class="perm-name">{{ permLabel(row.code, g.module) }}</span>
                    <span class="perm-code">{{ row.code }}</span>
                  </th>
                  @for (r of matrix().roles; track r.code) {
                    <td class="cell">
                      @if (row.granted.has(r.code)) {
                        <span class="dot" aria-hidden="true"></span>
                        <span class="sr-only">{{ r.code }} grants {{ row.code }}</span>
                      } @else {
                        <span class="sr-only">{{ r.code }} does not grant {{ row.code }}</span>
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          }
        </table>
      }
    </section>
  `,
  styles: `
    .matrix {
      min-width: 0;
    }
    .cell {
      text-align: center;
    }
    /* Row headers sit inside the row: same surface and the same 1px line as the cells beside them, so every row reads as one line. */
    tbody th[scope='row'] {
      background: transparent;
      border-bottom: 1px solid var(--line-soft);
      font-weight: 400;
      color: var(--text);
      white-space: normal;
    }
    tbody tr:last-child th[scope='row'] {
      border-bottom: 0;
    }
    .perm-name {
      display: block;
      font-family: var(--font-ui);
      font-size: 0.875rem;
      font-weight: 500;
    }
    .perm-code {
      display: block;
      font-family: var(--font-mono);
      font-size: 0.72rem;
      color: var(--muted);
    }
    /* Module rows: a quiet band in the same family as everything else, not shouting caps. */
    .module-row th {
      font-family: var(--font-ui);
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--text);
      background: color-mix(in oklab, var(--bg) 60%, var(--surface));
      border-top: 1px solid var(--line);
      border-bottom: 1px solid var(--line);
      padding-top: 0.55rem;
      padding-bottom: 0.55rem;
    }
    .module-row .count {
      font-weight: 400;
      color: var(--muted);
    }
    .dot {
      display: inline-block;
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: var(--accent);
    }
  `,
})
export class RolesMatrix {
  private readonly api = inject(RolesApi);
  protected readonly roles = injectQuery(() => ({
    queryKey: ['access', 'roles'] as const,
    queryFn: () => this.api.list(),
  }));
  protected readonly matrix = computed(() => buildMatrix(this.roles.data() ?? []));

  /** `CLAIMS` -> `Claims`. */
  protected moduleLabel(module: string): string {
    return titleCase(module.replace(/_/g, ' '));
  }

  /** `CLAIMS_EXPOSURE` under module `CLAIMS` -> `Exposure`; the code itself stays visible underneath. */
  protected permLabel(code: string, module: string): string {
    const rest = code.startsWith(module + '_') ? code.slice(module.length + 1) : code;
    return titleCase(rest.replace(/_/g, ' '));
  }
}

function titleCase(words: string): string {
  const lower = words.toLowerCase();
  return lower.charAt(0).toUpperCase() + lower.slice(1);
}
