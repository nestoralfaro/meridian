import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  async function renderWithVersion(response: object | 'error'): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const request = http.expectOne('/api/v1/version');
    if (response === 'error') {
      request.flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
    } else {
      request.flush(response);
    }
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the product name as the page heading', async () => {
    const page = await render();
    http.match('/api/v1/version');

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Meridian Client Portal');
  });

  it('states that this is a fictional demo with synthetic data', async () => {
    const page = await render();
    http.match('/api/v1/version');

    const disclaimer = page.querySelector('[data-testid="demo-disclaimer"]');
    expect(disclaimer?.textContent).toContain('Fictional demo');
    expect(disclaimer?.textContent).toContain('not investment advice');
  });

  it('renders routed content inside the main landmark', async () => {
    const page = await render();
    http.match('/api/v1/version');

    expect(page.querySelector('main router-outlet')).not.toBeNull();
  });

  it('shows the short commit of the running build, proving the SPA reaches the API', async () => {
    const page = await renderWithVersion({
      version: '1.0.0',
      commit: '0123456789abcdef0123456789abcdef01234567',
    });

    expect(page.querySelector('[data-testid="build-info"]')?.textContent?.trim()).toBe(
      'Build 0123456',
    );
  });

  it('omits build info quietly when the API is unavailable', async () => {
    const page = await renderWithVersion('error');

    expect(page.querySelector('[data-testid="build-info"]')).toBeNull();
  });
});
