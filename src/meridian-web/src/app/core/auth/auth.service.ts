import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { MeResponse, RegisterRequest, SignInRequest } from '../api';

export type AuthStatus = 'loading' | 'signed-in' | 'signed-out';
export type DemoPersonaKey = 'dana' | 'raj';

/** Outcome of a credential request: success, field errors (registration), or one message to show. */
export type CredentialResult =
  | { readonly ok: true }
  | { readonly ok: false; readonly errors: Readonly<Record<string, readonly string[]>> }
  | { readonly ok: false; readonly message: string };

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

  register(request: RegisterRequest): Promise<CredentialResult> {
    return this.credentialRequest('/api/v1/auth/register', request);
  }

  signIn(request: SignInRequest): Promise<CredentialResult> {
    return this.credentialRequest('/api/v1/auth/sign-in', request);
  }

  private async credentialRequest(url: string, body: unknown): Promise<CredentialResult> {
    await this.refreshAntiforgeryToken();
    try {
      await firstValueFrom(this.http.post<void>(url, body));
    } catch (error) {
      return toFailure(error);
    }
    await this.refreshAntiforgeryToken();
    this.session.reload();
    return { ok: true };
  }

  /** The API sets an XSRF-TOKEN cookie; Angular's HttpClient echoes it as X-XSRF-TOKEN on POST/PUT/DELETE. */
  refreshAntiforgeryToken(): Promise<unknown> {
    return firstValueFrom(this.http.get('/api/v1/auth/antiforgery'));
  }
}

interface ProblemBody {
  readonly title?: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
}

function toFailure(error: unknown): CredentialResult {
  const problem = error instanceof HttpErrorResponse ? (error.error as ProblemBody | null) : null;
  if (problem?.errors) {
    return { ok: false, errors: problem.errors };
  }
  return {
    ok: false,
    message: problem?.detail ?? problem?.title ?? 'Something went wrong. Please try again.',
  };
}
