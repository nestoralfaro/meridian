import { HttpClient, httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { MeResponse } from '../api';

export type AuthStatus = 'loading' | 'signed-in' | 'signed-out';
export type DemoPersonaKey = 'dana' | 'raj';

/**
 * The session lives in an HttpOnly cookie the SPA can't read (ADR-0004), so the SPA asks the API who's signed in.
 * `/api/v1/auth/me` answering 401 means signed out.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = httpResource<MeResponse>(() => '/api/v1/auth/me');

  /** 'loading' until /me has answered, so guards never decide on a guess. */
  readonly status = computed<AuthStatus>(() => {
    switch (this.session.status()) {
      case 'resolved':
      case 'local':
        return this.session.hasValue() ? 'signed-in' : 'signed-out';
      case 'error':
        return 'signed-out';
      default:
        return 'loading';
    }
  });

  readonly user = computed(() => (this.session.hasValue() ? this.session.value() : null));
  readonly firstName = computed(() => this.user()?.displayName.split(' ')[0] ?? null);

  async demoSignIn(persona: DemoPersonaKey): Promise<void> {
    await this.refreshAntiforgeryToken();
    await firstValueFrom(this.http.post<void>('/api/v1/auth/demo-sign-in', { persona }));
    // Antiforgery tokens are bound to the identity: get a new one for the signed-in user.
    await this.refreshAntiforgeryToken();
    this.session.reload();
  }

  /** The API sets an XSRF-TOKEN cookie; Angular's HttpClient echoes it as X-XSRF-TOKEN on POST/PUT/DELETE. */
  refreshAntiforgeryToken(): Promise<unknown> {
    return firstValueFrom(this.http.get('/api/v1/auth/antiforgery'));
  }
}
