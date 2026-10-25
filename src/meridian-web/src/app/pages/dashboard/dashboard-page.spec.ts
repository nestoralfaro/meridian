import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { AccountsService, type AccountsState } from '../../core/accounts/accounts.service';
import { DashboardPage } from './dashboard-page';

const usd = (amount: number) => ({ amount, currency: 'USD' });
const overview = {
  accounts: [
    {
      id: 'a1',
      name: 'Individual Brokerage',
      type: 'IndividualBrokerage',
      maskedNumber: '****4821',
      totalValue: usd(12480.55),
      cash: usd(1381.05),
      dayChange: usd(75.5),
      dayChangePercent: 0.61,
    },
    {
      id: 'a2',
      name: 'Roth IRA',
      type: 'RothIra',
      maskedNumber: '****7310',
      totalValue: usd(8301.2),
      cash: usd(189.2),
      dayChange: usd(-12.5),
      dayChangePercent: -0.15,
    },
  ],
  totalValue: usd(20781.75),
  dayChange: usd(63),
  dayChangePercent: 0.3,
  allocation: [
    { assetClass: 'Fund', value: usd(14718), percent: 70.82 },
    { assetClass: 'Stock', value: usd(6063.75), percent: 29.18 },
  ],
};

describe('DashboardPage', () => {
  const state = signal<AccountsState>({ status: 'loading' });
  const reload = vi.fn();

  beforeEach(() => {
    state.set({ status: 'loading' });
    reload.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { firstName: signal('Dana') } },
        { provide: AccountsService, useValue: { state, reload } },
      ],
    });
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows a busy placeholder while loading', () => {
    const page = render();

    expect(page.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(page.querySelector('.skeleton')).not.toBeNull();
  });

  it('lists each account with its balance and signed day change', () => {
    state.set({ status: 'loaded', overview });
    const page = render();

    const accounts = Array.from(page.querySelectorAll('[data-testid="account"]')).map((a) =>
      a.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(accounts[0]).toContain('Individual Brokerage');
    expect(accounts[0]).toContain('****4821');
    expect(accounts[0]).toContain('$12,480.55');
    expect(accounts[0]).toContain('+$75.50');
    expect(accounts[0]).toContain('+0.61%');
    expect(accounts[1]).toContain('−$12.50');
    expect(accounts[1]).toContain('−0.15%');
  });

  it('shows the total across accounts', () => {
    state.set({ status: 'loaded', overview });
    const page = render();

    expect(page.querySelector('[data-testid="total-value"]')?.textContent?.trim()).toBe(
      '$20,781.75',
    );
  });

  it('describes direction in words for screen readers, not just with color or arrows', () => {
    state.set({ status: 'loaded', overview });
    const page = render();

    const down = page.querySelectorAll('[data-testid="account"]')[1]!;
    expect(down.querySelector('.change')?.getAttribute('aria-label')).toBe(
      'Down $12.50 (0.15%) today',
    );
  });

  it('explains an error, offers "Try again", and shows a reference ID', () => {
    state.set({ status: 'error', referenceId: '4bf92f3577b34da6a3ce929d0e0e4736' });
    const page = render();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain(
      "couldn't load your accounts",
    );
    expect(page.textContent).toContain('4bf92f3577b34da6a3ce929d0e0e4736');
    page.querySelector<HTMLButtonElement>('button')!.click();
    expect(reload).toHaveBeenCalled();
  });

  it('welcomes clients who have no accounts yet', () => {
    state.set({
      status: 'loaded',
      overview: {
        accounts: [],
        totalValue: usd(0),
        dayChange: usd(0),
        dayChangePercent: 0,
        allocation: [],
      },
    });
    const page = render();

    expect(page.textContent).toContain("You don't have any accounts yet");
  });

  it('shows the asset allocation once accounts are loaded', () => {
    state.set({ status: 'loaded', overview });
    const page = render();

    expect(page.querySelector('mer-allocation-chart')).not.toBeNull();
    expect(page.querySelector('mer-allocation-chart tbody')?.textContent).toContain('Funds');
  });
});
