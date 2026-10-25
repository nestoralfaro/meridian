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

describe('AuthService credentials', () => {
  let http: HttpTestingController;
  let auth: AuthService;
  const nextMacrotask = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    TestBed.tick();
    http.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await TestBed.inject(ApplicationRef).whenStable();
  });

  afterEach(() => http.verify());

  async function answer(
    url: string,
    body: object | null,
    status: number,
    statusText: string,
  ): Promise<void> {
    await nextMacrotask();
    http.expectOne(url).flush(body, { status, statusText });
  }

  it('returns field errors when registration is rejected', async () => {
    const result = auth.register({
      displayName: 'Avery',
      email: 'a@example.test',
      password: 'short',
    });
    await answer('/api/v1/auth/antiforgery', null, 204, 'No Content');
    await answer(
      '/api/v1/auth/register',
      { errors: { password: ['Passwords must be at least 12 characters.'] } },
      400,
      'Bad Request',
    );

    expect(await result).toEqual({
      ok: false,
      errors: { password: ['Passwords must be at least 12 characters.'] },
    });
  });

  it('returns the server message when sign-in fails', async () => {
    const result = auth.signIn({ email: 'a@example.test', password: 'wrong wrong wrong' });
    await answer('/api/v1/auth/antiforgery', null, 204, 'No Content');
    await answer(
      '/api/v1/auth/sign-in',
      { detail: 'Sign-in failed. Check your email and password, or try again later.' },
      401,
      'Unauthorized',
    );

    expect(await result).toEqual({
      ok: false,
      message: 'Sign-in failed. Check your email and password, or try again later.',
    });
  });

  it('explains rate limiting', async () => {
    const result = auth.signIn({ email: 'a@example.test', password: 'wrong wrong wrong' });
    await answer('/api/v1/auth/antiforgery', null, 204, 'No Content');
    await answer(
      '/api/v1/auth/sign-in',
      { title: 'Too many attempts. Please wait a minute and try again.' },
      429,
      'Too Many Requests',
    );

    expect(await result).toEqual({
      ok: false,
      message: 'Too many attempts. Please wait a minute and try again.',
    });
  });
});
