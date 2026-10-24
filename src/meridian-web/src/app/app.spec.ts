import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the product name as the page heading', async () => {
    const page = await render();

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Meridian Client Portal');
  });

  it('states that this is a fictional demo with synthetic data', async () => {
    const page = await render();

    const disclaimer = page.querySelector('[data-testid="demo-disclaimer"]');
    expect(disclaimer?.textContent).toContain('Fictional demo');
    expect(disclaimer?.textContent).toContain('not investment advice');
  });

  it('renders routed content inside the main landmark', async () => {
    const page = await render();

    expect(page.querySelector('main router-outlet')).not.toBeNull();
  });
});
