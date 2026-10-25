import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { ActivityResponse } from '../../core/api';
import { AuthService } from '../../core/auth/auth.service';
import { formatChange } from './money';

const dateFormat = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
  timeZone: 'UTC',
});

/** The signed-in client's most recent transactions, newest first. */
@Component({
  selector: 'mer-activity-list',
  templateUrl: './activity-list.html',
  styleUrl: './activity-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivityList {
  readonly limit = input(5);
  private readonly auth = inject(AuthService);

  /** Keyed to the user (like the accounts overview) so nobody sees the previous user's activity. */
  private readonly activity = httpResource<ActivityResponse>(() =>
    this.auth.user() ? { url: '/api/v1/activity', params: { limit: this.limit() } } : undefined,
  );

  protected readonly items = computed(() =>
    this.activity.hasValue()
      ? this.activity.value().items.map((item) => ({
          ...item,
          displayDate: dateFormat.format(new Date(`${item.date}T00:00:00Z`)),
          displayAmount: formatChange(item.amount),
        }))
      : null,
  );

  protected readonly referenceId = computed(() => {
    const error = this.activity.error();
    return error instanceof HttpErrorResponse ? error.headers.get('X-Trace-Id') : error ? '' : null;
  });

  protected reload(): void {
    this.activity.reload();
  }
}
