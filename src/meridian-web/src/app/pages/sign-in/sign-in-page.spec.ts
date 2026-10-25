import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { SignInPage } from './sign-in-page';

describe('SignInPage', () => {
  const demoSignIn = vi.fn<(persona: string) => Promise<void>>();
  const signIn =
    vi.fn<(request: unknown) => Promise<{ ok: true } | { ok: false; message: string }>>();

  beforeEach(() => {
    demoSignIn.mockReset();
    signIn.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'sign-in', component: SignInPage },
          { path: 'dashboard', children: [] },
          { path: 'elsewhere', children: [] },
        ]),
        { provide: AuthService, useValue: { demoSignIn, signIn } },
      ],
    });
  });

  it('offers one-click sign-in for each demo persona', async () => {
    const harness = await RouterTestingHarness.create('/sign-in');
    const buttons = Array.from(harness.routeNativeElement!.querySelectorAll('.persona button')).map(
      (b) => b.textContent?.trim(),
    );

    expect(buttons).toEqual(['Sign in as Dana', 'Sign in as Raj']);
  });

  it('signs in as the chosen persona and goes to the dashboard', async () => {
    demoSignIn.mockResolvedValue();
    const harness = await RouterTestingHarness.create('/sign-in');

    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();

    expect(demoSignIn).toHaveBeenCalledWith('dana');
    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });

  it('returns to the page the visitor originally asked for', async () => {
    demoSignIn.mockResolvedValue();
    const harness = await RouterTestingHarness.create('/sign-in?returnUrl=%2Felsewhere');

    harness.routeNativeElement!.querySelectorAll<HTMLButtonElement>('button')[1]!.click();
    await harness.fixture.whenStable();

    expect(demoSignIn).toHaveBeenCalledWith('raj');
    expect(TestBed.inject(Router).url).toBe('/elsewhere');
  });

  it('ignores return URLs that point off-site', async () => {
    demoSignIn.mockResolvedValue();
    const harness = await RouterTestingHarness.create('/sign-in?returnUrl=https%3A%2F%2Fevil.test');

    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });

  it('announces a failure to assistive technology', async () => {
    demoSignIn.mockRejectedValue(new Error('down'));
    const harness = await RouterTestingHarness.create('/sign-in');

    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    const alert = harness.routeNativeElement!.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain("couldn't sign you in");
  });

  async function signInWith(
    harness: RouterTestingHarness,
    email: string,
    password: string,
  ): Promise<HTMLElement> {
    const page = harness.routeNativeElement!;
    const emailInput = page.querySelector<HTMLInputElement>('input[type="email"]')!;
    const passwordInput = page.querySelector<HTMLInputElement>('input[type="password"]')!;
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    page.querySelector<HTMLButtonElement>('form button[type="submit"]')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    return page;
  }

  it('signs in with email and password', async () => {
    signIn.mockResolvedValue({ ok: true });
    const harness = await RouterTestingHarness.create('/sign-in');

    await signInWith(harness, 'raj@example.test', 'a long and unusual passphrase');

    expect(signIn).toHaveBeenCalledWith({
      email: 'raj@example.test',
      password: 'a long and unusual passphrase',
    });
    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });

  it('announces the server message when sign-in fails', async () => {
    signIn.mockResolvedValue({
      ok: false,
      message: 'Sign-in failed. Check your email and password, or try again later.',
    });
    const harness = await RouterTestingHarness.create('/sign-in');

    const page = await signInWith(harness, 'raj@example.test', 'wrong');

    expect(page.querySelector('form [role="alert"]')?.textContent).toContain('Sign-in failed.');
    expect(TestBed.inject(Router).url).toContain('/sign-in');
  });

  it('links to account creation', async () => {
    const harness = await RouterTestingHarness.create('/sign-in');

    expect(harness.routeNativeElement!.querySelector('a[href="/register"]')?.textContent).toContain(
      'Create an account',
    );
  });
});
