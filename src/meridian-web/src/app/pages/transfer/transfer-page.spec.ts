import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AccountsService, type AccountsState } from '../../core/accounts/accounts.service';
import { TransferService, type TransferResult } from '../../core/transfers/transfer.service';
import { TransferPage, parseAmount } from './transfer-page';

const usd = (amount: number) => ({ amount, currency: 'USD' });
const account = (id: string, name: string, cash: number, version: number) => ({
  id,
  name,
  type: 'IndividualBrokerage',
  maskedNumber: '****0000',
  totalValue: usd(cash),
  cash: usd(cash),
  dayChange: usd(0),
  dayChangePercent: 0,
  version,
});
const loaded: AccountsState = {
  status: 'loaded',
  overview: {
    accounts: [
      account('b', 'Individual Brokerage', 1381.05, 7),
      account('r', 'Roth IRA', 189.2, 9),
    ],
    totalValue: usd(1570.25),
    dayChange: usd(0),
    dayChangePercent: 0,
    allocation: [],
  },
};

describe('parseAmount', () => {
  it.each([
    ['500', 500],
    ['1,250.50', 1250.5],
    ['$0.99', 0.99],
    [' 12.3 ', 12.3],
  ])('reads %s', (text, expected) => expect(parseAmount(text)).toBe(expected));

  it.each(['', 'abc', '-5', '1.001', '1,25', '1e3'])('rejects %j', (text) =>
    expect(parseAmount(text)).toBeNull(),
  );
});

describe('TransferPage', () => {
  const transfer = vi.fn<(request: unknown, key: string) => Promise<TransferResult>>();
  const reload = vi.fn();

  beforeEach(() => {
    transfer.mockReset();
    reload.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AccountsService, useValue: { state: signal(loaded), reload } },
        { provide: TransferService, useValue: { transfer } },
      ],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(TransferPage);
    const element = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      fixture.detectChanges();
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const fill = async (from: string, to: string, amount: string) => {
      const select = (id: string, value: string) => {
        const el = element.querySelector<HTMLSelectElement>(`#transfer-${id}`)!;
        el.value = value;
        el.dispatchEvent(new Event('change'));
      };
      select('fromAccountId', from);
      select('toAccountId', to);
      const input = element.querySelector<HTMLInputElement>('#transfer-amount')!;
      input.value = amount;
      input.dispatchEvent(new Event('input'));
      element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
      await settle();
    };
    const button = (name: RegExp) =>
      [...element.querySelectorAll<HTMLButtonElement>('button')].find((b) =>
        name.test(b.textContent ?? ''),
      )!;
    return { element, settle, fill, button };
  }

  it('checks the details before review, and focuses the first problem', async () => {
    const { element, fill } = await render();

    await fill('b', 'b', '2000');

    expect(element.textContent).toContain('Choose a different account to move money to.');
    expect(element.textContent).toContain('You have $1,381.05 available in Individual Brokerage.');
    expect(document.activeElement?.id).toBe('transfer-toAccountId');
    expect(transfer).not.toHaveBeenCalled();
  });

  it('reviews, then confirms with the reviewed version and one idempotency key', async () => {
    const { element, fill, button, settle } = await render();
    await fill('b', 'r', '500');

    expect(element.querySelector('h2')?.textContent).toContain('Review your transfer');
    expect(element.querySelector('.review')?.textContent).toContain('$500.00');

    transfer.mockResolvedValueOnce({
      kind: 'failed',
      message:
        "We couldn't confirm the transfer. Trying again is safe: it won't move your money twice.",
      referenceId: 'abc',
    });
    button(/Confirm transfer/).click();
    await settle();
    expect(element.textContent).toContain('Trying again is safe');

    transfer.mockResolvedValueOnce({
      kind: 'completed',
      transfer: {
        id: 't1',
        fromAccountId: 'b',
        toAccountId: 'r',
        amount: usd(500),
        requestedAt: '2026-10-29T15:00:00Z',
      },
    });
    button(/Try again/).click();
    await settle();

    expect(transfer).toHaveBeenCalledTimes(2);
    const [[firstRequest, firstKey], [, retryKey]] = transfer.mock.calls as [
      [unknown, string],
      [unknown, string],
    ];
    expect(firstRequest).toEqual({
      fromAccountId: 'b',
      toAccountId: 'r',
      amount: 500,
      fromAccountVersion: 7,
    });
    expect(retryKey).toBe(firstKey); // a retry can't move the money twice
    expect(element.querySelector('h2')?.textContent).toContain('Transfer complete');
    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      '$500.00 moved from Individual Brokerage to Roth IRA.',
    );
    expect(reload).toHaveBeenCalled();
  });

  it('sends the client back to the details when the balance changed', async () => {
    const { element, fill, button, settle } = await render();
    await fill('b', 'r', '10');
    transfer.mockResolvedValueOnce({
      kind: 'changed',
      message:
        'The balance changed since you reviewed this transfer. Review it again before confirming.',
    });

    button(/Confirm transfer/).click();
    await settle();

    expect(element.querySelector('h2')?.textContent).toContain('Transfer details');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('The balance changed');
    expect(reload).toHaveBeenCalled();
  });

  it('a new review gets a new idempotency key', async () => {
    const { fill, button, settle } = await render();
    transfer.mockResolvedValue({ kind: 'invalid', errors: { form: ['Refused.'] } });

    await fill('b', 'r', '10');
    button(/Confirm transfer/).click();
    await settle();
    await fill('b', 'r', '10');
    button(/Confirm transfer/).click();
    await settle();

    const [[, first], [, second]] = transfer.mock.calls as [[unknown, string], [unknown, string]];
    expect(second).not.toBe(first);
  });
});
