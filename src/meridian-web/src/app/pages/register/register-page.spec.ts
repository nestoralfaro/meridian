import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { AuthService, type CredentialResult } from '../../core/auth/auth.service';
import { RegisterPage } from './register-page';

describe('RegisterPage', () => {
  const register = vi.fn<(request: unknown) => Promise<CredentialResult>>();

  beforeEach(() => {
    register.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'register', component: RegisterPage },
          { path: 'dashboard', children: [] },
        ]),
        { provide: AuthService, useValue: { register } },
      ],
    });
  });

  async function fill(
    harness: RouterTestingHarness,
    values: { name: string; email: string; password: string },
  ): Promise<HTMLElement> {
    const page = harness.routeNativeElement!;
    for (const [label, value] of [
      ['Your name', values.name],
      ['Email', values.email],
      ['Password', values.password],
    ] as const) {
      const input = inputLabelled(page, label);
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    harness.detectChanges();
    return page;
  }

  function inputLabelled(page: HTMLElement, text: string): HTMLInputElement {
    const label = Array.from(page.querySelectorAll('label')).find((l) =>
      l.textContent?.trim().startsWith(text),
    );
    return page.querySelector<HTMLInputElement>(`#${label!.htmlFor}`)!;
  }

  it('labels every field and explains the password rule up front', async () => {
    const harness = await RouterTestingHarness.create('/register');
    const page = harness.routeNativeElement!;

    const password = page.querySelector<HTMLInputElement>('input[type="password"]')!;
    expect(page.querySelector(`label[for="${password.id}"]`)?.textContent).toContain('Password');
    expect(
      page.querySelector(`#${password.getAttribute('aria-describedby')!.split(' ')[0]}`)
        ?.textContent,
    ).toContain('At least 12 characters');
    expect(password.getAttribute('autocomplete')).toBe('new-password');
  });

  it('creates the account and goes to the dashboard', async () => {
    register.mockResolvedValue({ ok: true });
    const harness = await RouterTestingHarness.create('/register');
    const page = await fill(harness, {
      name: 'Avery Stone',
      email: 'avery@example.test',
      password: 'correct horse battery staple',
    });

    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await harness.fixture.whenStable();

    expect(register).toHaveBeenCalledWith({
      displayName: 'Avery Stone',
      email: 'avery@example.test',
      password: 'correct horse battery staple',
    });
    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });

  it('shows the server’s password guidance on the field and announces it', async () => {
    register.mockResolvedValue({
      ok: false,
      errors: { password: ['This password is too common. Choose something harder to guess.'] },
    });
    const harness = await RouterTestingHarness.create('/register');
    const page = await fill(harness, {
      name: 'Avery Stone',
      email: 'avery@example.test',
      password: 'password1234',
    });

    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    const password = page.querySelector<HTMLInputElement>('input[type="password"]')!;
    expect(password.getAttribute('aria-invalid')).toBe('true');
    const describedBy = password
      .getAttribute('aria-describedby')!
      .split(' ')
      .map((id) => page.querySelector(`#${id}`)?.textContent ?? '');
    expect(describedBy.join(' ')).toContain('too common');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('too common');
  });

  it('checks the length rule before calling the server', async () => {
    const harness = await RouterTestingHarness.create('/register');
    const page = await fill(harness, {
      name: 'Avery Stone',
      email: 'avery@example.test',
      password: 'password1',
    });

    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(register).not.toHaveBeenCalled();
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('at least 12 characters');
    expect(document.activeElement).toBe(page.querySelector('input[type="password"]'));
  });
});
