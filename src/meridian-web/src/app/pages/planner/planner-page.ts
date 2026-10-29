import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import type { SimulationRequest, SimulationResponse } from '../../core/api';
import { AuthService } from '../../core/auth/auth.service';
import { formatMoney } from '../../shared/ui/money';

type Field = keyof Omit<SimulationRequest, 'seed'>;

const FIELDS: readonly Field[] = [
  'currentAge',
  'retirementAge',
  'currentSavings',
  'monthlyContribution',
  'annualSpending',
  'stockPercent',
];

const WIDTH = 600;
const HEIGHT = 240;
const PAD = { top: 12, right: 12, bottom: 28, left: 64 };

const wholeDollars = (amount: number) =>
  formatMoney({ amount, currency: 'USD' }).replace(/\.00$/, '');

/** Retirement planner: inputs → probability that savings last, plus 10th/50th/90th percentile balances by age. */
@Component({
  selector: 'mer-planner-page',
  imports: [ReactiveFormsModule],
  templateUrl: './planner-page.html',
  styleUrl: './planner-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlannerPage {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);

  protected readonly form = inject(NonNullableFormBuilder).group({
    currentAge: [52],
    retirementAge: [67],
    currentSavings: [250000],
    monthlyContribution: [1500],
    annualSpending: [40000],
    stockPercent: [60],
  });

  protected readonly busy = signal(false);
  protected readonly result = signal<SimulationResponse | null>(null);
  protected readonly errors = signal<Partial<Record<Field | 'form', readonly string[]>>>({});
  protected readonly summary = computed(() => Object.values(this.errors()).flat());

  protected readonly successPercent = computed(() => {
    const result = this.result();
    return result ? Math.round(result.successRate * 100) : null;
  });

  /** SVG paths for the fan chart: the 10th–90th percentile band and the median line. */
  protected readonly chart = computed(() => {
    const result = this.result();
    if (!result || result.years.length < 2) {
      return null;
    }
    const years = result.years;
    const max = Math.max(...years.map((y) => y.p90), 1);
    const first = years[0]!.age;
    const last = years[years.length - 1]!.age;
    const x = (age: number) =>
      PAD.left + ((age - first) / (last - first)) * (WIDTH - PAD.left - PAD.right);
    const y = (value: number) =>
      HEIGHT - PAD.bottom - (value / max) * (HEIGHT - PAD.top - PAD.bottom);
    const line = (points: readonly (readonly [number, number])[]) =>
      points
        .map(([px, py], i) => `${i === 0 ? 'M' : 'L'}${px.toFixed(1)},${py.toFixed(1)}`)
        .join(' ');

    const upper = years.map((p) => [x(p.age), y(p.p90)] as const);
    const lower = [...years].reverse().map((p) => [x(p.age), y(p.p10)] as const);
    const ticks = [0, 0.5, 1].map((f) => ({ y: y(max * f), label: compact(max * f) }));
    const ageTicks = years
      .filter((p) => (p.age - first) % 10 === 0 || p.age === last)
      .map((p) => ({ x: x(p.age), label: String(p.age) }));
    return {
      band: `${line(upper)} ${line(lower).replace(/^M/, 'L')} Z`,
      median: line(years.map((p) => [x(p.age), y(p.p50)] as const)),
      ticks,
      ageTicks,
      baseline: HEIGHT - PAD.bottom,
      left: PAD.left,
      right: WIDTH - PAD.right,
    };
  });

  /** The data table shows every fifth year (and the last) to stay readable. */
  protected readonly tableRows = computed(() => {
    const result = this.result();
    if (!result) {
      return [];
    }
    const first = result.years[0]?.age ?? 0;
    return result.years
      .filter((p, i) => (p.age - first) % 5 === 0 || i === result.years.length - 1)
      .map((p) => ({
        age: p.age,
        p10: wholeDollars(p.p10),
        p50: wholeDollars(p.p50),
        p90: wholeDollars(p.p90),
      }));
  });

  protected readonly viewBox = `0 0 ${WIDTH} ${HEIGHT}`;

  protected errorsFor(field: Field): readonly string[] {
    return this.errors()[field] ?? [];
  }

  protected async run(): Promise<void> {
    const values = this.form.getRawValue();
    const local = clientErrors(values);
    if (Object.keys(local).length > 0) {
      this.showErrors(local);
      return;
    }
    this.errors.set({});
    this.busy.set(true);
    try {
      await this.auth.refreshAntiforgeryToken();
      const result = await firstValueFrom(
        this.http.post<SimulationResponse>('/api/v1/planner/simulations', values),
      );
      this.result.set(result);
      afterNextRender(() => this.document.getElementById('planner-result')?.focus(), {
        injector: this.injector,
      });
    } catch (error) {
      const problem = error instanceof HttpErrorResponse ? error.error : null;
      this.showErrors(
        problem?.errors ?? { form: ["We couldn't run the plan right now. Please try again."] },
      );
    } finally {
      this.busy.set(false);
    }
  }

  private showErrors(errors: Partial<Record<string, readonly string[]>>): void {
    this.errors.set(errors);
    const first = FIELDS.find((f) => errors[f]?.length);
    afterNextRender(
      () =>
        this.document.getElementById(first ? `planner-${first}` : 'planner-error-summary')?.focus(),
      { injector: this.injector },
    );
  }
}

function clientErrors(v: Omit<SimulationRequest, 'seed'>): Partial<Record<Field, string[]>> {
  const errors: Partial<Record<Field, string[]>> = {};
  const whole = (n: unknown) => typeof n === 'number' && Number.isInteger(n);
  if (!whole(v.currentAge) || v.currentAge < 18 || v.currentAge > 90) {
    errors.currentAge = ['Enter an age from 18 to 90.'];
  }
  if (!whole(v.retirementAge) || v.retirementAge < v.currentAge || v.retirementAge > 94) {
    errors.retirementAge = ['Enter a retirement age from your current age to 94.'];
  }
  for (const [field, max] of [
    ['currentSavings', 100_000_000],
    ['monthlyContribution', 1_000_000],
    ['annualSpending', 10_000_000],
  ] as const) {
    const n = v[field];
    if (typeof n !== 'number' || Number.isNaN(n) || n < 0 || n > max) {
      errors[field] = [`Enter an amount from $0 to ${wholeDollars(max)}.`];
    }
  }
  if (!whole(v.stockPercent) || v.stockPercent < 0 || v.stockPercent > 100) {
    errors.stockPercent = ['Enter a percentage from 0 to 100.'];
  }
  return errors;
}

/** "$1.2M", "$350K": short axis labels. */
function compact(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(amount);
}
