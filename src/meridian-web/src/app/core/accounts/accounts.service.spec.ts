import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../auth/auth.service';
import { AccountsService } from './accounts.service';

describe('AccountsService', () => {
  const user = signal<{ displayName: string; email: string; isDemo: boolean } | null>(null);
  const empty = {
    accounts: [],
    totalValue: { amount: 0, currency: 'USD' },
    dayChange: { amount: 0, currency: 'USD' },
    dayChangePercent: 0,
  };
  let http: HttpTestingController;
  let accounts: AccountsService;

  beforeEach(() => {
    user.set({ displayName: 'Dana Lee', email: 'dana@meridian.test', isDemo: true });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it("drops one user's data when they sign out and refetches for the next user", async () => {
    TestBed.tick();
    http
      .expectOne('/api/v1/accounts')
      .flush({ ...empty, totalValue: { amount: 20781.75, currency: 'USD' } });
    await settle();
    expect(accounts.state().status).toBe('loaded');

    user.set(null);
    TestBed.tick();
    expect(accounts.state().status).toBe('loading'); // nothing of Dana's remains
    http.expectNone('/api/v1/accounts');

    user.set({ displayName: 'Raj Mehta', email: 'raj@meridian.test', isDemo: true });
    TestBed.tick();
    http.expectOne('/api/v1/accounts').flush(empty);
    await settle();
    expect(accounts.state()).toEqual({ status: 'loaded', overview: empty });
  });

  it('exposes the trace id of a failed request as the reference ID', async () => {
    TestBed.tick();
    http.expectOne('/api/v1/accounts').flush(null, {
      status: 500,
      statusText: 'Server Error',
      headers: { 'X-Trace-Id': '4bf92f3577b34da6a3ce929d0e0e4736' },
    });
    await settle();

    expect(accounts.state()).toEqual({
      status: 'error',
      referenceId: '4bf92f3577b34da6a3ce929d0e0e4736',
    });
  });
});
