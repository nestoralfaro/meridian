import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import type { GetVersionData, VersionResponse } from '../api';

/** Typed against the API contract: renaming the route or schema breaks the build, not production. */
const VERSION_URL: GetVersionData['url'] = '/api/v1/version';

/**
 * Server state as a signal (ADR-0007): `httpResource` loads once and exposes value/error/loading as signals.
 * Build info is decorative, so a failed request yields `null` rather than an error the UI must handle.
 */
@Injectable({ providedIn: 'root' })
export class BuildInfoService {
  /** Same origin as the SPA, so a relative URL is enough (ADR-0004). */
  private readonly build = httpResource<VersionResponse>(() => VERSION_URL);

  readonly shortCommit = computed(() =>
    this.build.hasValue() ? this.build.value().commit.slice(0, 7) : null,
  );
}
