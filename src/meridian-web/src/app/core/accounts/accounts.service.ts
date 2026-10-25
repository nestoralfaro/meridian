import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import type { AccountsOverviewResponse } from '../api';
import { AuthService } from '../auth/auth.service';

export type AccountsState =
  | { readonly status: 'loading' }
  | { readonly status: 'loaded'; readonly overview: AccountsOverviewResponse }
  | { readonly status: 'error'; readonly referenceId: string | null };

/** The signed-in client's accounts overview (ADR-0007: server state as a resource). */
@Injectable({ providedIn: 'root' })
export class AccountsService {
  private readonly auth = inject(AuthService);

  /**
   * Keyed to the signed-in user: a different user (or signing out) drops the previous data and refetches,
   * so one person's balances are never shown to the next person using the same browser.
   */
  private readonly overview = httpResource<AccountsOverviewResponse>(() => {
    const user = this.auth.user();
    // A new request object per user: the resource refetches whenever the user changes.
    return user ? { url: '/api/v1/accounts' } : undefined;
  });

  readonly state = computed<AccountsState>(() => {
    if (this.overview.hasValue()) {
      return { status: 'loaded', overview: this.overview.value() };
    }
    const error = this.overview.error();
    if (error) {
      // The trace id lets support find this exact request in Application Insights.
      const referenceId =
        error instanceof HttpErrorResponse ? error.headers.get('X-Trace-Id') : null;
      return { status: 'error', referenceId };
    }
    return { status: 'loading' };
  });

  reload(): void {
    this.overview.reload();
  }
}
