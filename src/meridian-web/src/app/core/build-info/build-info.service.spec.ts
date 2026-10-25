import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BuildInfoService } from './build-info.service';

describe('BuildInfoService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('exposes the running build as a signal, loaded from the versioned API on the same origin', async () => {
    const service = TestBed.inject(BuildInfoService);
    TestBed.tick();

    const request = http.expectOne('/api/v1/version');
    expect(request.request.method).toBe('GET');
    request.flush({ version: '1.0.0', commit: '0123456789abcdef0123456789abcdef01234567' });
    await TestBed.inject(ApplicationRef).whenStable();

    expect(service.shortCommit()).toBe('0123456');
  });

  it('yields no commit (rather than throwing) when the API fails', async () => {
    const service = TestBed.inject(BuildInfoService);
    TestBed.tick();

    http
      .expectOne('/api/v1/version')
      .flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
    await TestBed.inject(ApplicationRef).whenStable();

    expect(service.shortCommit()).toBeNull();
  });
});
