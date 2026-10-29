import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { AccountPage, FILTER_DEBOUNCE_MS } from './account-page';

const usd = (amount: number) => ({ amount, currency: 'USD' });
const accountId = 'a1';
const detail = {
  id: accountId,
  name: 'Individual Brokerage',
  type: 'IndividualBrokerage',
  maskedNumber: '****2296',
  totalValue: usd(127462),
  cash: usd(15240),
  dayChange: usd(492),
  dayChangePercent: 0.39,
  holdings: [],
};
const position = (symbol: string, name: string, marketValue: number, gain: number) => ({
  symbol,
  name,
  assetClass: 'Fund',
  quantity: 10,
  price: usd(marketValue / 10),
  marketValue: usd(marketValue),
  costBasis: usd(marketValue - gain),
  unrealizedGain: usd(gain),
  unrealizedGainPercent: gain === 0 ? null : 1.5,
  dayChange: usd(-1),
});
const pageOf = (items: object[], page = 1, totalCount = items.length, totalPages = 1) => ({
  items,
  page,
  pageSize: 10,
  totalCount,
  totalPages,
});

describe('AccountPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            user: signal({ displayName: 'Raj Mehta', email: 'raj@meridian.test', isDemo: true }),
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  async function settle(fixture: { detectChanges(): void }): Promise<void> {
    fixture.detectChanges();
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
  }

  /** Runs effects so a resource sends its request (settle() would wait on the open request). */
  function kick(fixture: { detectChanges(): void }): void {
    fixture.detectChanges();
    TestBed.tick();
  }

  function expectPositionsRequest(expected: Record<string, string>) {
    const request = http.expectOne((r) => r.url === `/api/v1/accounts/${accountId}/positions`);
    for (const [key, value] of Object.entries(expected)) {
      expect(request.request.params.get(key), key).toBe(value);
    }
    return request;
  }

  async function render(positions = pageOf([position('MDTOT', 'Total Market', 73590, 12390)])) {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.componentRef.setInput('accountId', accountId);
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne(`/api/v1/accounts/${accountId}`).flush(detail);
    expectPositionsRequest({ page: '1', pageSize: '10', sort: 'marketValue', order: 'desc' }).flush(
      positions,
    );
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows the account and its positions, largest first by default', async () => {
    const { element } = await render();

    expect(element.querySelector('h1')?.textContent).toContain('Individual Brokerage');
    expect(element.querySelector('[data-testid="account-total"]')?.textContent).toContain(
      '$127,462.00',
    );
    const row = element.querySelector('[data-testid="position"]')!;
    expect(row.querySelector('th')?.textContent).toContain('MDTOT');
    expect(row.textContent).toContain('$73,590.00');
    expect(row.textContent).toContain('+$12,390.00');
    const active = element.querySelector('th[aria-sort]')!;
    expect(active.textContent).toContain('Market value');
    expect(active.getAttribute('aria-sort')).toBe('descending');
  });

  it('sorts on the server: a new text column starts A→Z, the same column flips', async () => {
    const { fixture, element } = await render();
    const symbolHeader = () =>
      [...element.querySelectorAll<HTMLElement>('thead th')].find((th) =>
        th.textContent?.includes('Symbol'),
      )!;

    symbolHeader().querySelector('button')!.click();
    kick(fixture);
    expectPositionsRequest({ sort: 'symbol', order: 'asc', page: '1' }).flush(pageOf([]));
    await settle(fixture);

    expect(element.textContent).toContain('This account holds only cash.');
  });

  it('flips the order when the active column is chosen again', async () => {
    const { fixture, element } = await render();

    element.querySelector<HTMLButtonElement>('th[aria-sort] button')!.click();
    kick(fixture);
    expectPositionsRequest({ sort: 'marketValue', order: 'asc' }).flush(
      pageOf([position('ACME', 'Acme', 7725, 825)]),
    );
    await settle(fixture);

    expect(element.querySelector('th[aria-sort]')?.getAttribute('aria-sort')).toBe('ascending');
  });

  it('filters after typing pauses, from page 1, and says when nothing matches', async () => {
    const { fixture, element } = await render(
      pageOf([position('MDTOT', 'Total Market', 1, 0)], 1, 12, 2),
    );
    element.querySelector<HTMLButtonElement>('nav button:last-of-type')!.click();
    kick(fixture);
    expectPositionsRequest({ page: '2' }).flush(
      pageOf([position('GLBX', 'Globex', 1, 0)], 2, 12, 2),
    );
    await settle(fixture);

    vi.useFakeTimers(); // only around the debounce: Angular's whenStable() can't settle with fake timers
    const input = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    input.value = 'zz';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(FILTER_DEBOUNCE_MS - 1);
    TestBed.tick();
    http.expectNone((r) => r.params.get('filter') === 'zz'); // still typing
    vi.advanceTimersByTime(1);
    vi.useRealTimers();
    kick(fixture);
    expectPositionsRequest({ filter: 'zz', page: '1' }).flush(pageOf([]));
    await settle(fixture);

    expect(element.textContent).toContain('No positions match “zz”.');
    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      'No positions matching “zz”.',
    );
  });

  it('pages with Previous/Next and announces where you are', async () => {
    const { fixture, element } = await render(
      pageOf([position('MDTOT', 'Total Market', 1, 0)], 1, 12, 2),
    );

    const [previous, next] = element.querySelectorAll<HTMLButtonElement>(
      'nav[aria-label="Positions pages"] button',
    );
    expect(previous!.disabled).toBe(true);
    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      '12 positions, sorted by market value, descending. Page 1 of 2.',
    );

    next!.click();
    kick(fixture);
    expectPositionsRequest({ page: '2' }).flush(
      pageOf([position('GLBX', 'Globex', 1, 0)], 2, 12, 2),
    );
    await settle(fixture);

    expect(element.querySelector('nav span')?.textContent).toContain('Page 2 of 2');
  });

  it("explains a missing account (or someone else's) without detail", async () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.componentRef.setInput('accountId', accountId);
    fixture.detectChanges();
    TestBed.tick();
    http
      .expectOne(`/api/v1/accounts/${accountId}`)
      .flush({ status: 404 }, { status: 404, statusText: 'Not Found' });
    expectPositionsRequest({}).flush({ status: 404 }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain("We couldn't find that account.");
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });
});
