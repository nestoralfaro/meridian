import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import {
  QUOTES_CONNECTION_FACTORY,
  type LiveUpdate,
  type QuotesConnection,
} from '../../core/quotes/live-quotes';
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

/** A stand-in for the SignalR connection: tests push updates and flip connection states. */
class FakeConnection implements QuotesConnection {
  readonly handlers: { positions?: (u: LiveUpdate) => void; reconnecting?: () => void } = {};
  watched: string[] = [];
  watchResult = true;
  on(_: 'positions', handler: (u: LiveUpdate) => void) {
    this.handlers.positions = handler;
  }
  onreconnecting(handler: () => void) {
    this.handlers.reconnecting = handler;
  }
  onreconnected() {
    // not exercised
  }
  onclose() {
    // not exercised
  }
  start() {
    return Promise.resolve();
  }
  stop() {
    return Promise.resolve();
  }
  invoke(_: 'WatchAccount', accountId: string) {
    this.watched.push(accountId);
    return Promise.resolve(this.watchResult);
  }
}

describe('AccountPage', () => {
  let http: HttpTestingController;
  let connection: FakeConnection;

  beforeEach(() => {
    connection = new FakeConnection();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: QUOTES_CONNECTION_FACTORY, useValue: () => Promise.resolve(connection) },
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

  it('shows live values computed on the server, and says when they pause', async () => {
    const { fixture, element } = await render();
    expect(connection.watched).toEqual([accountId]);
    expect(element.querySelector('[data-testid="live-status"]')?.textContent).toContain(
      'Prices as of the last close',
    );

    connection.handlers.positions!({
      asOf: '2026-10-29T15:04:05Z',
      totalValue: usd(127500.12),
      dayChange: usd(530.12),
      positions: [
        {
          symbol: 'MDTOT',
          price: usd(245.42),
          marketValue: usd(73626),
          dayChange: usd(696),
          unrealizedGain: usd(12426),
          unrealizedGainPercent: 20.3,
        },
      ],
    });
    await settle(fixture);

    const row = element.querySelector('[data-testid="position"]')!;
    expect(row.textContent).toContain('$245.42');
    expect(row.textContent).toContain('$73,626.00');
    expect(row.textContent).toContain('+$12,426.00');
    expect(element.querySelector('[data-testid="account-total"]')?.textContent).toContain(
      '$127,500.12',
    );
    expect(element.querySelector('[data-testid="live-status"]')?.textContent).toContain(
      'Live prices',
    );

    connection.handlers.reconnecting!();
    await settle(fixture);
    expect(element.querySelector('[data-testid="live-status"]')?.textContent).toContain(
      'Prices paused, reconnecting',
    );
  });
});
