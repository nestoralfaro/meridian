import {
  DOCUMENT,
  DestroyRef,
  Injectable,
  InjectionToken,
  Injector,
  effect,
  inject,
  signal,
} from '@angular/core';
import { AuthService } from './auth.service';

export interface SessionTimeoutConfig {
  /** Idle time before automatic sign-out. Matches the server's sliding session cookie (15 minutes). */
  readonly idleMs: number;
  /** How long before sign-out the warning appears. */
  readonly warnBeforeMs: number;
}

export const SESSION_TIMEOUT = new InjectionToken<SessionTimeoutConfig>('SESSION_TIMEOUT', {
  factory: () => ({ idleMs: 15 * 60_000, warnBeforeMs: 2 * 60_000 }),
});

/** User input that counts as activity. Background requests don't: an open tab alone shouldn't keep a session alive. */
const ACTIVITY_EVENTS = ['keydown', 'pointerdown', 'wheel', 'touchstart'] as const;

/**
 * Protects clients who walk away from a shared computer: after a period without input, warns, then signs out and
 * says why. While the warning shows, only an explicit "Stay signed in" keeps the session.
 */
@Injectable({ providedIn: 'root' })
export class SessionTimeoutService {
  private readonly auth = inject(AuthService);
  private readonly config = inject(SESSION_TIMEOUT);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  readonly warning = signal(false);
  readonly secondsLeft = signal(0);

  private warnTimer?: ReturnType<typeof setTimeout>;
  private signOutTimer?: ReturnType<typeof setTimeout>;
  private countdown?: ReturnType<typeof setInterval>;
  private armed = false;
  private readonly onActivity = () => {
    if (this.armed && !this.warning()) {
      this.restart();
    }
  };

  start(): void {
    effect(
      () => {
        if (this.auth.status() === 'signed-in') {
          this.arm();
        } else {
          this.disarm();
        }
      },
      { injector: this.injector },
    );
    this.destroyRef.onDestroy(() => this.disarm());
  }

  async staySignedIn(): Promise<void> {
    await this.auth.extendSession();
    this.restart();
  }

  signOutNow(): Promise<void> {
    this.disarm();
    return this.auth.signOut();
  }

  private arm(): void {
    if (!this.armed) {
      this.armed = true;
      ACTIVITY_EVENTS.forEach((event) =>
        this.document.addEventListener(event, this.onActivity, { passive: true }),
      );
    }
    this.restart();
  }

  private disarm(): void {
    this.armed = false;
    ACTIVITY_EVENTS.forEach((event) => this.document.removeEventListener(event, this.onActivity));
    this.clearTimers();
    this.warning.set(false);
  }

  private restart(): void {
    this.clearTimers();
    this.warning.set(false);
    const { idleMs, warnBeforeMs } = this.config;
    this.warnTimer = setTimeout(() => this.warn(), idleMs - warnBeforeMs);
    this.signOutTimer = setTimeout(() => this.expire(), idleMs);
  }

  private warn(): void {
    this.secondsLeft.set(Math.round(this.config.warnBeforeMs / 1000));
    this.warning.set(true);
    this.countdown = setInterval(() => this.secondsLeft.update((s) => Math.max(0, s - 1)), 1000);
  }

  private expire(): void {
    this.disarm();
    void this.auth.signOut('idle');
  }

  private clearTimers(): void {
    clearTimeout(this.warnTimer);
    clearTimeout(this.signOutTimer);
    clearInterval(this.countdown);
  }
}
