import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { routes } from './app.routes';

describe('routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes, withComponentInputBinding())],
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

    expect(page.querySelector('h2')?.textContent).toContain('Welcome');
    expect(title).toBe('Meridian Client Portal');
  });

  it('renders the dashboard for a deep link', async () => {
    const { page, title } = await navigate('/dashboard');

    expect(page.querySelector('h2')?.textContent).toContain('Dashboard');
    expect(title).toBe('Dashboard | Meridian');
  });

  it('renders a not-found page for unknown routes', async () => {
    const { page, title } = await navigate('/no-such-page');

    expect(page.querySelector('h2')?.textContent).toContain('Page not found');
    expect(title).toBe('Page not found | Meridian');
  });
});
