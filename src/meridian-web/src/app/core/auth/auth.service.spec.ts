import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  /** Lets awaited HTTP calls inside the service continue to the next request. */
  const nextMacrotask = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

  async function settle(): Promise<void> {
    await TestBed.inject(ApplicationRef).whenStable();
  }

  it('is loading until the session is known', () => {
    TestBed.tick();
    expect(auth.status()).toBe('loading');
    http.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
  });

  it('treats 401 from /me as signed out', async () => {
    TestBed.tick();
    http.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(auth.status()).toBe('signed-out');
    expect(auth.user()).toBeNull();
  });

  it('exposes the signed-in user and their first name', async () => {
    TestBed.tick();
    http
      .expectOne('/api/v1/auth/me')
      .flush({ displayName: 'Dana Lee', email: 'dana@meridian.test', isDemo: true });
    await settle();

    expect(auth.status()).toBe('signed-in');
    expect(auth.firstName()).toBe('Dana');
  });

  it('signs in as a demo persona with a fresh antiforgery token, then reloads the session', async () => {
    TestBed.tick();
    http.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    const signingIn = auth.demoSignIn('dana');
    http
      .expectOne('/api/v1/auth/antiforgery')
      .flush(null, { status: 204, statusText: 'No Content' });
    await nextMacrotask();
    const post = http.expectOne('/api/v1/auth/demo-sign-in');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ persona: 'dana' });
    post.flush(null, { status: 204, statusText: 'No Content' });
    await nextMacrotask();
    // Antiforgery tokens are bound to the identity, so a new one is fetched after signing in.
    http
      .expectOne('/api/v1/auth/antiforgery')
      .flush(null, { status: 204, statusText: 'No Content' });
    await signingIn;
    TestBed.tick();
    http
      .expectOne('/api/v1/auth/me')
      .flush({ displayName: 'Dana Lee', email: 'dana@meridian.test', isDemo: true });
    await settle();

    expect(auth.status()).toBe('signed-in');
  });
});
