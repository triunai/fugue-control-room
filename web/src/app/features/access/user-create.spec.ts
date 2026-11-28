import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { AccessApi } from '../../api/access.api';
import type { UserDto } from '../../api/generated/model';
import { RolesApi } from '../../api/roles.api';
import { ApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { isStaleEdit, placeProblem, toCreateUserRequest, toggleRole, toUpdateUserRequest } from './user-create.logic';
import { UserCreateDrawer } from './user-create-drawer';

const problem = (
  status: number,
  code: string | null,
  errors: Record<string, string[]> = {},
  detail: string | null = null,
) => new ApiProblem(status, 'refused', code, detail, errors, 'corr-1', null, null, 'POST', '/api/v1/users');

const created: UserDto = {
  id: '00000000-0000-0000-0000-000000000009',
  email: 'new@example.test',
  displayName: 'New Person',
  isActive: true,
  roles: ['STAFF'],
  groups: [],
  createdAt: '2026-11-28T00:00:00Z',
  version: 1,
};

describe('user create logic', () => {
  it('toggles a role on and off, sorted and without duplicates', () => {
    expect(toggleRole([], 'STAFF', true)).toEqual(['STAFF']);
    expect(toggleRole(['STAFF'], 'ADMIN', true)).toEqual(['ADMIN', 'STAFF']);
    expect(toggleRole(['STAFF'], 'STAFF', true)).toEqual(['STAFF']);
    expect(toggleRole(['ADMIN', 'STAFF'], 'ADMIN', false)).toEqual(['STAFF']);
  });

  it('trims the email and name but sends the password exactly as typed', () => {
    const body = toCreateUserRequest({
      email: ' a@b.test ',
      displayName: ' A ',
      password: ' spaced pass ',
      roles: ['STAFF'],
    }); // secrets-scan:ignore
    expect(body).toEqual({ email: 'a@b.test', displayName: 'A', password: ' spaced pass ', roles: ['STAFF'] }); // secrets-scan:ignore
  });

  it('puts a taken email on the email field and an escalation on the roles, anything else nowhere', () => {
    expect(placeProblem(problem(409, 'user.email_taken'))?.field).toBe('email');
    expect(placeProblem(problem(403, 'user.role_escalation', {}, 'You cannot assign ADMIN.'))).toEqual({
      field: 'roles',
      message: 'You cannot assign ADMIN.',
    });
    expect(placeProblem(problem(403, null))).toBeNull();
    expect(placeProblem(problem(409, 'idempotency.in_progress'))).toBeNull();
    expect(placeProblem(problem(409, 'user.self_lockout'))?.field).toBe('roles');
  });

  it('builds an edit body with no email and no password, and knows a 412 is a stale edit', () => {
    expect(toUpdateUserRequest({ displayName: ' A ', isActive: false, roles: ['STAFF'] })).toEqual({
      displayName: 'A',
      isActive: false,
      roles: ['STAFF'],
    });
    expect(isStaleEdit(problem(412, 'user.version_mismatch'))).toBe(true);
    expect(isStaleEdit(problem(409, 'user.email_taken'))).toBe(false);
  });
});

describe('Add user drawer', () => {
  function setup(createUser: (body: unknown) => Promise<UserDto>) {
    const close = vi.fn();
    const create = vi.fn(createUser);
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => new QueryClient()),
        { provide: DialogRef, useValue: { close } },
        { provide: AccessApi, useValue: { createUser: create } },
        {
          provide: RolesApi,
          useValue: {
            list: () =>
              Promise.resolve([
                { code: 'ADMIN', name: 'Administrator', permissions: [] },
                { code: 'STAFF', name: 'Staff', permissions: [] },
              ]),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(UserCreateDrawer);
    const root = fixture.nativeElement as HTMLElement;
    const type = (id: string, value: string) => {
      const input = root.querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const fillValid = () => {
      type('nu-email', ' new@example.test ');
      type('nu-name', 'New Person');
      type('nu-password', 'long-enough-pass');
      root.querySelectorAll<HTMLInputElement>('[data-testid="role-option"] input')[1].click(); // STAFF
    };
    const submit = () => root.querySelector<HTMLFormElement>('#user-form')!.dispatchEvent(new Event('submit'));
    return { fixture, root, close, create, settle, fillValid, submit, type };
  }

  it('shows and hides the initial password with a pressed-state button', async () => {
    const { root, settle } = setup(() => Promise.resolve(created));
    await settle();
    const input = root.querySelector<HTMLInputElement>('#nu-password')!;
    const toggle = root.querySelector<HTMLButtonElement>('[data-testid="toggle-password"]')!;
    expect(input.type).toBe('password');
    expect(toggle.getAttribute('aria-pressed')).toBe('false');
    expect(toggle.getAttribute('aria-label')).toBe('Show password');

    toggle.click();
    await settle();
    expect(input.type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    expect(toggle.getAttribute('aria-label')).toBe('Hide password');
  });

  it('blocks an empty form on the client with a message per field', async () => {
    const { root, create, settle, submit } = setup(() => Promise.resolve(created));
    await settle();

    submit();
    await settle();

    expect(create).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Enter an email address.');
    expect(root.textContent).toContain('Enter a name.');
    expect(root.textContent).toContain('Enter an initial password.');
    expect(root.textContent).toContain('Pick at least one role.');
  });

  it('flags a short password before sending', async () => {
    const { root, create, settle, fillValid, submit, type } = setup(() => Promise.resolve(created));
    await settle();
    fillValid();
    type('nu-password', 'short');

    submit();
    await settle();

    expect(create).not.toHaveBeenCalled();
    expect(root.textContent).toContain('At least 12 characters.');
  });

  it('sends the trimmed body with the checked roles, toasts and closes with the created user', async () => {
    const { close, create, settle, fillValid, submit } = setup(() => Promise.resolve(created));
    const toasts = TestBed.inject(ToastStore);
    const ok = vi.spyOn(toasts, 'ok');
    await settle();
    fillValid();

    submit();
    await settle();

    expect(create).toHaveBeenCalledWith({
      email: 'new@example.test',
      displayName: 'New Person',
      password: 'long-enough-pass', // secrets-scan:ignore
      roles: ['STAFF'],
    });
    expect(ok).toHaveBeenCalledWith('new@example.test created', expect.any(String), expect.anything());
    expect(close).toHaveBeenCalledWith(created);
  });

  it('shows the API field errors inline and a taken email under the email field, and stays open', async () => {
    const answers = [
      problem(400, 'validation.failed', { password: ['Password is too common.'] }), // secrets-scan:ignore
      problem(409, 'user.email_taken'),
    ];
    const { root, close, settle, fillValid, submit, type } = setup(() => Promise.reject(answers.shift()));
    await settle();
    fillValid();

    submit();
    await settle();
    expect(root.querySelector('#nu-password-msg')?.textContent).toContain('Password is too common.');

    type('nu-password', 'a-different-long-pass'); // editing the field clears the server error
    submit();
    await settle();
    expect(root.querySelector('#nu-email-msg')?.textContent).toContain('A user with this email already exists.');
    expect(close).not.toHaveBeenCalled();
  });
});

describe('Edit user drawer', () => {
  const existing: UserDto = { ...created, displayName: 'Old Name', version: 41 };

  function setup(updateUser: (id: string, body: unknown, version: number) => Promise<UserDto>, user = existing) {
    const close = vi.fn();
    const update = vi.fn(updateUser);
    const read = vi.fn(() => Promise.resolve({ ...existing, displayName: 'Saved Elsewhere', version: 42 }));
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => new QueryClient()),
        { provide: DialogRef, useValue: { close } },
        { provide: DIALOG_DATA, useValue: user },
        { provide: AccessApi, useValue: { createUser: vi.fn(), updateUser: update, user: read } },
        {
          provide: RolesApi,
          useValue: {
            list: () =>
              Promise.resolve([
                { code: 'ADMIN', name: 'Administrator', permissions: [] },
                { code: 'STAFF', name: 'Staff', permissions: [] },
              ]),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(UserCreateDrawer);
    const root = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const type = (id: string, value: string) => {
      const input = root.querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const submit = () => root.querySelector<HTMLFormElement>('#user-form')!.dispatchEvent(new Event('submit'));
    return { root, close, update, read, settle, type, submit };
  }

  it('prefills the user, shows the email read-only and offers no password field', async () => {
    const { root, settle } = setup(() => Promise.resolve(existing));
    await settle();

    expect(root.textContent).toContain('Edit user');
    const email = root.querySelector<HTMLInputElement>('#nu-email')!;
    expect(email.value).toBe('new@example.test');
    expect(email.disabled).toBe(true);
    expect(root.querySelector<HTMLInputElement>('#nu-name')!.value).toBe('Old Name');
    expect(root.querySelector('#nu-password')).toBeNull();
    expect(root.querySelector('[data-testid="toggle-password"]')).toBeNull();
    const checked = [...root.querySelectorAll<HTMLInputElement>('[data-testid="role-option"] input')].map(
      (i) => i.checked,
    );
    expect(checked).toEqual([false, true]); // STAFF
  });

  it('saves with PUT at the version it opened, toasts and closes with the saved user', async () => {
    const saved = { ...existing, displayName: 'New Name', roles: ['ADMIN', 'STAFF'], version: 42 };
    const { root, close, update, settle, type, submit } = setup(() => Promise.resolve(saved));
    await settle();
    type('nu-name', ' New Name ');
    root.querySelectorAll<HTMLInputElement>('[data-testid="role-option"] input')[0].click(); // + ADMIN

    submit();
    await settle();

    expect(update).toHaveBeenCalledWith(
      existing.id,
      { displayName: 'New Name', isActive: true, roles: ['ADMIN', 'STAFF'] },
      41,
    );
    expect(close).toHaveBeenCalledWith(saved);
  });

  it('on 412 says someone else changed it, stays open, and Reload refills and edits against the new version', async () => {
    const answers: unknown[] = [problem(412, 'user.version_mismatch')];
    const { root, close, update, read, settle, submit } = setup(() =>
      answers.length ? Promise.reject(answers.shift()) : Promise.resolve(existing),
    );
    await settle();

    submit();
    await settle();
    expect(root.querySelector('[data-testid="stale-banner"]')?.textContent).toContain('Changed by someone else.');
    expect(close).not.toHaveBeenCalled();

    root.querySelector<HTMLButtonElement>('[data-testid="reload-user"]')!.click();
    await settle();
    expect(read).toHaveBeenCalledWith(existing.id);
    expect(root.querySelector<HTMLInputElement>('#nu-name')!.value).toBe('Saved Elsewhere');
    expect(root.querySelector('[data-testid="stale-banner"]')).toBeNull();

    submit();
    await settle();
    expect(update).toHaveBeenLastCalledWith(
      existing.id,
      expect.objectContaining({ displayName: 'Saved Elsewhere' }),
      42,
    );
  });
});
