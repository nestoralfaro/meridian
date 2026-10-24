import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Mirrors the API's `GET /api/v1/version` response. */
export interface BuildInfo {
  readonly version: string;
  readonly commit: string;
}

@Injectable({ providedIn: 'root' })
export class BuildInfoService {
  private readonly http = inject(HttpClient);

  /** The API is served from the same origin as the SPA, so a relative URL is enough. */
  get(): Observable<BuildInfo> {
    return this.http.get<BuildInfo>('/api/v1/version');
  }
}
