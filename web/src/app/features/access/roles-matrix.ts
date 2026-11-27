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
                <th scope="colgroup" [attr.colspan]="matrix().roles.length + 1">{{ g.module }}</th>
              </tr>
              @for (row of g.rows; track row.code) {
                <tr>
                  <th scope="row" class="mono perm">{{ row.code }}</th>
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
    .perm {
      font-weight: 500;
      font-size: 0.8rem;
    }
    .module-row th {
      background: var(--surface);
      font-size: 0.7rem;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--accent-text);
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
}
