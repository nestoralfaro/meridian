import type { MoneyResponse } from '../../core/api';

const formatters = new Map<string, Intl.NumberFormat>();

function formatterFor(currency: string): Intl.NumberFormat {
  let formatter = formatters.get(currency);
  if (!formatter) {
    formatter = new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
    formatters.set(currency, formatter);
  }
  return formatter;
}

/** "$20,781.75". Display only: all arithmetic happens server-side in decimal. */
export function formatMoney(money: MoneyResponse): string {
  return formatterFor(money.currency).format(money.amount);
}

/** "+$75.50" / "−$12.50" (a true minus sign), so direction is in the text, not only in color. */
export function formatChange(money: MoneyResponse): string {
  const magnitude = formatMoney({ ...money, amount: Math.abs(money.amount) });
  return money.amount > 0 ? `+${magnitude}` : money.amount < 0 ? `−${magnitude}` : magnitude;
}

export function formatPercentChange(percent: number): string {
  const magnitude = `${Math.abs(percent).toFixed(2)}%`;
  return percent > 0 ? `+${magnitude}` : percent < 0 ? `−${magnitude}` : magnitude;
}

/** Spoken form for screen readers: "Up $75.50 (0.61%) today". */
export function describeChange(money: MoneyResponse, percent: number): string {
  if (money.amount === 0) {
    return 'No change today';
  }
  const direction = money.amount > 0 ? 'Up' : 'Down';
  return `${direction} ${formatMoney({ ...money, amount: Math.abs(money.amount) })} (${Math.abs(percent).toFixed(2)}%) today`;
}
