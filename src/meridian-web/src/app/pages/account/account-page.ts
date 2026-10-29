import { HttpErrorResponse, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AccountDetailResponse, PositionsPageResponse } from '../../core/api';
import { AuthService } from '../../core/auth/auth.service';
import {
  describeChange,
  formatChange,
  formatMoney,
  formatPercentChange,
} from '../../shared/ui/money';

/** Server-side sort keys (an allow-list on the API too). */
export type PositionSort =
  'symbol' | 'name' | 'quantity' | 'price' | 'marketValue' | 'unrealizedGain' | 'dayChange';
export type SortOrder = 'asc' | 'desc';

interface Column {
  readonly key: PositionSort;
  readonly label: string;
  readonly numeric: boolean;
}

export const PAGE_SIZE = 10;
export const FILTER_DEBOUNCE_MS = 300;

const quantityFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 4 });

/** One account: its value, and its positions sorted, filtered and paged on the server. */
@Component({
  selector: 'mer-account-page',
  imports: [RouterLink],
  templateUrl: './account-page.html',
  styleUrl: './account-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPage {
  /** From the route (`/accounts/:accountId`). */
  readonly accountId = input.required<string>();

  private readonly auth = inject(AuthService);

  protected readonly columns: readonly Column[] = [
    { key: 'symbol', label: 'Symbol', numeric: false },
    { key: 'name', label: 'Name', numeric: false },
    { key: 'quantity', label: 'Quantity', numeric: true },
    { key: 'price', label: 'Price', numeric: true },
    { key: 'marketValue', label: 'Market value', numeric: true },
    { key: 'unrealizedGain', label: 'Gain/loss', numeric: true },
    { key: 'dayChange', label: 'Day change', numeric: true },
  ];

  protected readonly sort = signal<PositionSort>('marketValue');
  protected readonly order = signal<SortOrder>('desc');
  protected readonly page = signal(1);
  /** Applied filter (debounced from what's typed). */
  protected readonly filter = signal('');

  // Both resources are keyed to the user, like the dashboard, so a different user never sees this data.
  private readonly account = httpResource<AccountDetailResponse>(() =>
    this.auth.user()
      ? { url: `/api/v1/accounts/${encodeURIComponent(this.accountId())}` }
      : undefined,
  );

  private readonly positions = httpResource<PositionsPageResponse>(() =>
    this.auth.user()
      ? {
          url: `/api/v1/accounts/${encodeURIComponent(this.accountId())}/positions`,
          params: {
            page: this.page(),
            pageSize: PAGE_SIZE,
            sort: this.sort(),
            order: this.order(),
            ...(this.filter() ? { filter: this.filter() } : {}),
          },
        }
      : undefined,
  );

  protected readonly detail = computed(() =>
    this.account.hasValue() ? this.account.value() : null,
  );

  protected readonly accountProblem = computed(() => problemOf(this.account.error()));

  /** The last page that loaded stays on screen while the next one loads, so the table doesn't jump. */
  protected readonly rows = computed(() => {
    if (!this.positions.hasValue()) {
      return null;
    }
    const value = this.positions.value();
    return {
      ...value,
      items: value.items.map((p) => ({
        ...p,
        displayQuantity: quantityFormat.format(p.quantity),
        displayPrice: formatMoney(p.price),
        displayMarketValue: formatMoney(p.marketValue),
        displayGain: formatChange(p.unrealizedGain),
        displayGainPercent:
          p.unrealizedGainPercent === null ? '' : formatPercentChange(p.unrealizedGainPercent),
        displayDayChange: formatChange(p.dayChange),
      })),
    };
  });

  protected readonly positionsLoading = this.positions.isLoading;
  protected readonly positionsProblem = computed(() => problemOf(this.positions.error()));

  /** Announced politely after each change so screen-reader users know the table updated. */
  protected readonly summary = computed(() => {
    const rows = this.rows();
    if (!rows) {
      return '';
    }
    const what = rows.totalCount === 1 ? 'position' : 'positions';
    const matching = this.filter() ? ` matching “${this.filter()}”` : '';
    if (rows.totalCount === 0) {
      return `No ${what}${matching}.`;
    }
    const sortedBy = this.columns.find((c) => c.key === this.sort())!.label.toLowerCase();
    const direction = this.order() === 'asc' ? 'ascending' : 'descending';
    return `${rows.totalCount} ${what}${matching}, sorted by ${sortedBy}, ${direction}. Page ${rows.page} of ${Math.max(rows.totalPages, 1)}.`;
  });

  protected readonly money = formatMoney;
  protected readonly change = formatChange;
  protected readonly percent = formatPercentChange;
  protected readonly describe = describeChange;

  private filterTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.filterTimer));
  }

  /** Same column flips the order; a new column starts where it's most useful (A→Z, largest first). */
  protected sortBy(column: Column): void {
    if (this.sort() === column.key) {
      this.order.update((o) => (o === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sort.set(column.key);
      this.order.set(column.numeric ? 'desc' : 'asc');
    }
    this.page.set(1);
  }

  protected ariaSort(column: Column): 'ascending' | 'descending' | null {
    return this.sort() === column.key
      ? this.order() === 'asc'
        ? 'ascending'
        : 'descending'
      : null;
  }

  protected onFilterInput(value: string): void {
    clearTimeout(this.filterTimer);
    this.filterTimer = setTimeout(() => {
      this.filter.set(value.trim());
      this.page.set(1);
    }, FILTER_DEBOUNCE_MS);
  }

  protected goTo(page: number): void {
    this.page.set(page);
  }

  protected reload(): void {
    this.account.reload();
    this.positions.reload();
  }
}

type Problem = { readonly notFound: boolean; readonly referenceId: string | null } | null;

function problemOf(error: unknown): Problem {
  if (!error) {
    return null;
  }
  return error instanceof HttpErrorResponse
    ? { notFound: error.status === 404, referenceId: error.headers.get('X-Trace-Id') }
    : { notFound: false, referenceId: null };
}
