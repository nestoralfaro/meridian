import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService, type AuthStatus } from './auth.service';
import { SESSION_TIMEOUT, SessionTimeoutService } from './session-timeout.service';

describe('SessionTimeoutService', () => {
  const minutes = (n: number) => n * 60_000;
  const status = signal<AuthStatus>('signed-in');
  const signOut = vi.fn<(reason?: string) => Promise<void>>();
  const extendSession = vi.fn<() => Promise<void>>();
  let service: SessionTimeoutService;

  beforeEach(() => {
    vi.useFakeTimers();
    status.set('signed-in');
    signOut.mockReset().mockResolvedValue();
    extendSession.mockReset().mockResolvedValue();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { status, signOut, extendSession } },
        { provide: SESSION_TIMEOUT, useValue: { idleMs: minutes(15), warnBeforeMs: minutes(2) } },
      ],
    });
    service = TestBed.inject(SessionTimeoutService);
    service.start();
    TestBed.tick();
  });

  afterEach(() => vi.useRealTimers());

  it('warns two minutes before the idle timeout', () => {
    vi.advanceTimersByTime(minutes(13) - 1);
    expect(service.warning()).toBe(false);

    vi.advanceTimersByTime(1);
    expect(service.warning()).toBe(true);
  });

  it('signs out after fifteen idle minutes, giving the reason', () => {
    vi.advanceTimersByTime(minutes(15));

    expect(signOut).toHaveBeenCalledWith('idle');
  });

  it('treats keyboard and pointer input as activity', () => {
    vi.advanceTimersByTime(minutes(10));
    document.dispatchEvent(new KeyboardEvent('keydown'));
    vi.advanceTimersByTime(minutes(10));

    expect(service.warning()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });

  it('"stay signed in" renews the server session and restarts the clock', async () => {
    vi.advanceTimersByTime(minutes(13));
    expect(service.warning()).toBe(true);

    await service.staySignedIn();
    vi.advanceTimersByTime(minutes(12));

    expect(extendSession).toHaveBeenCalledOnce();
    expect(service.warning()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });

  it('does nothing while signed out', () => {
    status.set('signed-out');
    TestBed.tick();
    vi.advanceTimersByTime(minutes(30));

    expect(service.warning()).toBe(false);
    expect(signOut).not.toHaveBeenCalled();
  });
});
