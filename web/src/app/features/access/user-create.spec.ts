import { DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { AccessApi } from '../../api/access.api';
import type { UserDto } from '../../api/generated/model';
import { RolesApi } from '../../api/roles.api';
import { ApiProblem } from '../../core/http/problem';
import { ToastStore } from '../../core/ui/toast.store';
import { placeProblem, toCreateUserRequest, toggleRole } from './user-create.logic';
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
