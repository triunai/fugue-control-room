import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessService } from './access.service';
import { HasPermissionDirective } from './has-permission.directive';

class FakeAccess {
  readonly granted = signal<string[]>([]);
  readonly permissions = computed(() => new Set(this.granted()));
  has(code: string | readonly string[]): boolean {
    const set = this.permissions();
    return typeof code === 'string' ? set.has(code) : code.some((c) => set.has(c));
  }
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HasPermissionDirective],
  template: `
    <button *hasPermission="'CUSTOMERS_DELETE'" id="delete">Delete</button>
    <span *hasPermission="['GROUPS_VIEW', 'OVERRIDES_VIEW']; else denied" id="access">Access</span>
    <ng-template #denied><span id="denied">No access</span></ng-template>
  `,
})
class Host {}

describe('HasPermissionDirective', () => {
  let access: FakeAccess;

  beforeEach(() => {
    access = new FakeAccess();
    TestBed.configureTestingModule({ providers: [{ provide: AccessService, useValue: access }] });
  });

  function render() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing (or the else template) without the permission code', () => {
    const el: HTMLElement = render().nativeElement;
    expect(el.querySelector('#delete')).toBeNull();
    expect(el.querySelector('#access')).toBeNull();
    expect(el.querySelector('#denied')).not.toBeNull();
  });

  it('renders with the code, and accepts any one of a list', () => {
    access.granted.set(['CUSTOMERS_DELETE', 'OVERRIDES_VIEW']);
    const el: HTMLElement = render().nativeElement;
    expect(el.querySelector('#delete')).not.toBeNull();
    expect(el.querySelector('#access')).not.toBeNull();
    expect(el.querySelector('#denied')).toBeNull();
  });

  it('re-evaluates when /me changes (a module switched off removes its codes)', async () => {
    access.granted.set(['CUSTOMERS_DELETE']);
    const fixture = render();
    expect(fixture.nativeElement.querySelector('#delete')).not.toBeNull();

    access.granted.set([]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#delete')).toBeNull();
  });

  it('never looks at role names: a role-like string grants nothing', () => {
    access.granted.set(['ADMIN']);
    expect(render().nativeElement.querySelector('#delete')).toBeNull();
  });
});
