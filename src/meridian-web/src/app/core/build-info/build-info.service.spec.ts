import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BuildInfoService } from './build-info.service';

describe('BuildInfoService', () => {
  let service: BuildInfoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BuildInfoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the running build from the versioned API on the same origin', () => {
    let received: unknown;
    service.get().subscribe((info) => (received = info));

    const request = http.expectOne('/api/v1/version');
    expect(request.request.method).toBe('GET');
    request.flush({ version: '1.0.0', commit: '0123456789abcdef0123456789abcdef01234567' });

    expect(received).toEqual({
      version: '1.0.0',
      commit: '0123456789abcdef0123456789abcdef01234567',
    });
  });
});
