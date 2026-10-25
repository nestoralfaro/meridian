import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App shell', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const aBuild = { version: '1.0.0', commit: '0123456789abcdef0123456789abcdef01234567' };

  /** Renders the shell and answers its build-info request (an unanswered httpResource keeps the app unstable). */
  async function render(version: object | 'error' = aBuild): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    TestBed.tick();
    const request = http.expectOne('/api/v1/version');
    if (version === 'error') {
      request.flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
    } else {
      request.flush(version);
    }
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('makes "Skip to main content" the first focusable element, targeting the main region', async () => {
    const page = await render();

    const firstFocusable = page.querySelector<HTMLElement>(
      'a[href], button, input, [tabindex]:not([tabindex="-1"])',
    );
    expect(firstFocusable?.textContent?.trim()).toBe('Skip to main content');
    expect(firstFocusable?.getAttribute('href')).toBe('#main-content');
    const main = page.querySelector('main#main-content');
    expect(main?.getAttribute('tabindex')).toBe('-1');
  });

  it('links the brand home with an accessible name', async () => {
    const page = await render();

    const brand = page.querySelector('header a.brand');
    expect(brand?.getAttribute('aria-label')).toBe('Meridian home');
    expect(brand?.getAttribute('href')).toBe('/');
  });

  it('offers labelled primary navigation', async () => {
    const page = await render();

    const nav = page.querySelector('nav[aria-label="Primary"]');
    const links = Array.from(nav?.querySelectorAll('a') ?? []).map((a) => a.textContent?.trim());
    expect(links).toEqual(['Home', 'Dashboard']);
  });

  it('renders routed content inside the main landmark', async () => {
    const page = await render();

    expect(page.querySelector('main router-outlet')).not.toBeNull();
  });

  it('states that this is a fictional demo with synthetic data', async () => {
    const page = await render();

    const disclaimer = page.querySelector('[data-testid="demo-disclaimer"]');
    expect(disclaimer?.textContent).toContain('Fictional demo');
    expect(disclaimer?.textContent).toContain('not investment advice');
  });

  it('shows the short commit of the running build, proving the SPA reaches the API', async () => {
    const page = await render({
      version: '1.0.0',
      commit: '0123456789abcdef0123456789abcdef01234567',
    });

    expect(page.querySelector('[data-testid="build-info"]')?.textContent?.trim()).toBe(
      'Build 0123456',
    );
  });

  it('omits build info quietly when the API is unavailable', async () => {
    const page = await render('error');

    expect(page.querySelector('[data-testid="build-info"]')).toBeNull();
  });
});
