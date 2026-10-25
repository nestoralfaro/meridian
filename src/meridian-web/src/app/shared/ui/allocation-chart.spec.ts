import { TestBed } from '@angular/core/testing';
import { AllocationChart } from './allocation-chart';

const usd = (amount: number) => ({ amount, currency: 'USD' });
const slices = [
  { assetClass: 'Fund', value: usd(14718), percent: 70.82 },
  { assetClass: 'Bond', value: usd(3206), percent: 15.43 },
  { assetClass: 'Cash', value: usd(1570.25), percent: 7.56 },
  { assetClass: 'Stock', value: usd(1287.5), percent: 6.19 },
];

describe('AllocationChart', () => {
  function render(input = slices): HTMLElement {
    const fixture = TestBed.createComponent(AllocationChart);
    fixture.componentRef.setInput('slices', input);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('lists every asset class with its value and share in a data table', () => {
    const page = render();

    const rows = Array.from(page.querySelectorAll('tbody tr')).map((row) =>
      Array.from(row.children).map((cell) => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['Funds', '$14,718.00', '70.82%'],
      ['Bonds', '$3,206.00', '15.43%'],
      ['Cash', '$1,570.25', '7.56%'],
      ['Stocks', '$1,287.50', '6.19%'],
    ]);
    expect(page.querySelector('th[scope="row"]')).not.toBeNull();
  });

  it('draws one donut segment per asset class, hidden from assistive technology (the table carries the data)', () => {
    const page = render();

    const svg = page.querySelector('svg')!;
    expect(svg.getAttribute('aria-hidden')).toBe('true');
    expect(svg.querySelectorAll('circle.donut__segment')).toHaveLength(4);
  });

  it('sizes segments by share, leaving a gap between them so each meets contrast against the background', () => {
    const page = render();

    const first = page
      .querySelector('circle.donut__segment')!
      .getAttribute('stroke-dasharray')!
      .split(' ')
      .map(Number);
    expect(first[0]).toBeCloseTo(70.82 - 0.6, 2); // segment length minus the gap
    expect(first[0]! + first[1]!).toBeCloseTo(100, 5); // the circle's circumference is 100
  });

  it('draws a single full ring without gaps for one asset class', () => {
    const page = render([{ assetClass: 'Cash', value: usd(48000), percent: 100 }]);

    const ring = page
      .querySelector('circle.donut__segment')!
      .getAttribute('stroke-dasharray')!
      .split(' ')
      .map(Number);
    expect(ring[0]).toBe(100);
  });
});
