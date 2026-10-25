import { formatChange, formatMoney, formatPercentChange } from './money';

describe('money formatting', () => {
  it('formats amounts as US currency with two decimals', () => {
    expect(formatMoney({ amount: 20781.75, currency: 'USD' })).toBe('$20,781.75');
    expect(formatMoney({ amount: 0, currency: 'USD' })).toBe('$0.00');
    expect(formatMoney({ amount: 1381.05, currency: 'USD' })).toBe('$1,381.05');
  });

  it('always signs changes, so direction never depends on color', () => {
    expect(formatChange({ amount: 75.5, currency: 'USD' })).toBe('+$75.50');
    expect(formatChange({ amount: -12.5, currency: 'USD' })).toBe('−$12.50');
    expect(formatChange({ amount: 0, currency: 'USD' })).toBe('$0.00');
  });

  it('formats percentage changes the same way', () => {
    expect(formatPercentChange(0.61)).toBe('+0.61%');
    expect(formatPercentChange(-1.2)).toBe('−1.20%');
    expect(formatPercentChange(0)).toBe('0.00%');
  });
});
