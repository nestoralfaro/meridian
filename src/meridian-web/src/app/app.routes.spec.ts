import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideRouter } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { routes } from './app.routes';
import { AuthService, type AuthStatus } from './core/auth/auth.service';

describe('routes', () => {
  const status = signal<AuthStatus>('signed-out');

  beforeEach(() => {
    status.set('signed-out');
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        {
          provide: AuthService,
          useValue: { status, firstName: signal('Dana'), demoSignIn: () => Promise.resolve() },
        },
      ],
    });
  });

  async function navigate(url: string): Promise<{ page: HTMLElement; title: string }> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return {
      page: harness.routeNativeElement as HTMLElement,
      title: TestBed.inject(Title).getTitle(),
    };
  }

  it('renders the landing page at the root', async () => {
    const { page, title } = await navigate('/');

    expect(page.querySelector('h1')?.textContent).toContain('Welcome');
    expect(title).toBe('Meridian Client Portal');
  });

  it('renders the dashboard for a signed-in user', async () => {
    status.set('signed-in');
    const { page, title } = await navigate('/dashboard');

    expect(page.querySelector('h1')?.textContent).toContain('Dashboard');
    expect(page.textContent).toContain('Welcome back, Dana');
    expect(title).toBe('Dashboard | Meridian');
  });

  it('sends anonymous visitors from the dashboard to sign in', async () => {
    const { page, title } = await navigate('/dashboard');

    expect(page.querySelector('h1')?.textContent).toContain('Sign in');
    expect(title).toBe('Sign in | Meridian');
  });

  it('renders a not-found page for unknown routes', async () => {
    const { page, title } = await navigate('/no-such-page');

    expect(page.querySelector('h1')?.textContent).toContain('Page not found');
    expect(title).toBe('Page not found | Meridian');
  });
});
