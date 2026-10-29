import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { CreateTransferRequest, MoneyResponse, TransferResponse } from '../api';
import { AuthService } from '../auth/auth.service';

export type TransferResult =
  | { readonly kind: 'completed'; readonly transfer: TransferResponse }
  /** The source account changed since review (409): reload and review again. */
  | { readonly kind: 'changed'; readonly message: string }
  | {
      readonly kind: 'insufficient-cash';
      readonly message: string;
      readonly available: MoneyResponse | null;
    }
  | { readonly kind: 'invalid'; readonly errors: Readonly<Record<string, readonly string[]>> }
  /** Network failure or 5xx: safe to retry with the same idempotency key. */
  | { readonly kind: 'failed'; readonly message: string; readonly referenceId: string | null };

interface ProblemBody {
  readonly title?: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
  readonly availableCash?: MoneyResponse;
}

@Injectable({ providedIn: 'root' })
export class TransferService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  /**
   * Sends the transfer with the caller's idempotency key. Retrying with the same key (after a timeout, say)
   * returns the original transfer instead of moving the money again.
   */
  async transfer(request: CreateTransferRequest, idempotencyKey: string): Promise<TransferResult> {
    try {
      await this.auth.refreshAntiforgeryToken();
      const transfer = await firstValueFrom(
        this.http.post<TransferResponse>('/api/v1/transfers', request, {
          headers: { 'Idempotency-Key': idempotencyKey },
        }),
      );
      return { kind: 'completed', transfer };
    } catch (error) {
      return toResult(error);
    }
  }
}

function toResult(error: unknown): TransferResult {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      kind: 'failed',
      message: 'Something went wrong. Please try again.',
      referenceId: null,
    };
  }
  const problem = (error.error ?? {}) as ProblemBody;
  switch (error.status) {
    case 409:
      return {
        kind: 'changed',
        message: problem.detail ?? 'Your account changed. Review the transfer again.',
      };
    case 422:
      if (problem.availableCash) {
        return {
          kind: 'insufficient-cash',
          message: problem.detail ?? 'Not enough cash.',
          available: problem.availableCash,
        };
      }
      return {
        kind: 'invalid',
        errors: { form: [problem.detail ?? problem.title ?? 'This transfer was refused.'] },
      };
    case 400:
    case 404:
      return {
        kind: 'invalid',
        errors: problem.errors ?? { form: [problem.title ?? 'This transfer was refused.'] },
      };
    default:
      return {
        kind: 'failed',
        message:
          "We couldn't confirm the transfer. Trying again is safe: it won't move your money twice.",
        referenceId: error.headers?.get('X-Trace-Id') ?? null,
      };
  }
}
