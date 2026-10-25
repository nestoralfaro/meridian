import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';

/** Mirrors the API's `GET /api/v1/version` response. */
export interface BuildInfo {
  readonly version: string;
  readonly commit: string;
}

/**
 * Server state as a signal (ADR-0007): `httpResource` loads once and exposes value/error/loading as signals.
 * Build info is decorative, so a failed request yields `null` rather than an error the UI must handle.
 */
@Injectable({ providedIn: 'root' })
export class BuildInfoService {
  /** Same origin as the SPA, so a relative URL is enough (ADR-0004). */
  private readonly build = httpResource<BuildInfo>(() => '/api/v1/version');

  readonly shortCommit = computed(() =>
    this.build.hasValue() ? this.build.value().commit.slice(0, 7) : null,
  );
}
