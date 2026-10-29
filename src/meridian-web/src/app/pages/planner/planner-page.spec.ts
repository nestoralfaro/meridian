import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../../core/auth/auth.service';
import { PlannerPage } from './planner-page';

const response = {
  successRate: 0.8234,
  runs: 2000,
  endAge: 95,
  years: Array.from({ length: 44 }, (_, i) => ({
    age: 52 + i,
    p10: 100000 + i * 1000,
    p50: 250000 + i * 5000,
    p90: 400000 + i * 10000,
  })),
  assumptions: { stockMean: 0.05, stockVolatility: 0.16, bondMean: 0.015, bondVolatility: 0.06 },
};

describe('PlannerPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { refreshAntiforgeryToken: () => Promise.resolve() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(PlannerPage);
    const settle = async () => {
      fixture.detectChanges();
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    return { fixture, element: fixture.nativeElement as HTMLElement, settle };
  }

  it('explains invalid inputs next to the field and sends nothing', async () => {
    const { element, settle } = await render();
    const retirement = element.querySelector<HTMLInputElement>('#planner-retirementAge')!;
    retirement.value = '40';
    retirement.dispatchEvent(new Event('input'));

    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle();

    expect(retirement.getAttribute('aria-invalid')).toBe('true');
    expect(element.textContent).toContain('Enter a retirement age from your current age to 94.');
    expect(document.activeElement).toBe(retirement);
  });

  it('runs the plan and shows the probability, the chart and the numbers', async () => {
    const { element, settle } = await render();

    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await Promise.resolve(); // let the antiforgery refresh resolve
    await Promise.resolve();
    const request = http.expectOne('/api/v1/planner/simulations');
    expect(request.request.body).toEqual({
      currentAge: 52,
      retirementAge: 67,
      currentSavings: 250000,
      monthlyContribution: 1500,
      annualSpending: 40000,
      stockPercent: 60,
    });
    request.flush(response);
    await settle();

    expect(element.querySelector('h2')?.textContent).toContain('82%');
    expect(element.querySelector('h2')?.textContent).toContain(
      'chance your savings last to age 95',
    );
    expect(element.querySelector('svg[role="img"]')?.getAttribute('aria-labelledby')).toBe(
      'fan-caption',
    );
    expect(element.querySelector('.fan__band')?.getAttribute('d')).toMatch(/^M.+Z$/);
    const rows = [...element.querySelectorAll('tbody tr')].map((r) =>
      r.querySelector('th')?.textContent?.trim(),
    );
    expect(rows).toEqual(['52', '57', '62', '67', '72', '77', '82', '87', '92', '95']);
    expect(element.querySelector('tbody td')?.textContent).toBe('$100,000');
  });
});
