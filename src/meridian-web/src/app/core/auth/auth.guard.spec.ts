import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService, type AuthStatus } from './auth.service';

describe('authGuard', () => {
  const status = signal<AuthStatus>('loading');

  beforeEach(() => {
    status.set('loading');
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { status } }],
    });
  });

  function run(url: string): Promise<boolean | UrlTree> {
    return TestBed.runInInjectionContext(
      () => authGuard({} as never, { url } as never) as Promise<boolean | UrlTree>,
    );
  }

  it('lets signed-in users through', async () => {
    status.set('signed-in');
    expect(await run('/dashboard')).toBe(true);
  });

  it('sends signed-out visitors to sign in, remembering where they were going', async () => {
    status.set('signed-out');
    const result = await run('/dashboard');

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(
      '/sign-in?returnUrl=%2Fdashboard',
    );
  });

  it('waits until the session is known', async () => {
    const decision = run('/dashboard');
    status.set('signed-in');
    TestBed.tick();

    expect(await decision).toBe(true);
  });
});
