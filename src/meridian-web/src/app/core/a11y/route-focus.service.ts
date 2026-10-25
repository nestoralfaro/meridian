import { DOCUMENT, DestroyRef, Injectable, Injector, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, skip } from 'rxjs';

/**
 * After client-side navigation, moves keyboard and screen-reader focus to the new page's main heading,
 * as a full page load would. Without this, focus stays on the clicked link and the new content goes unannounced.
 * The first navigation (initial page load) is left alone so focus starts at the top of the document.
 */
@Injectable({ providedIn: 'root' })
export class RouteFocusService {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  start(): void {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        skip(1),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => afterNextRender(() => this.focusHeading(), { injector: this.injector }));
  }

  private focusHeading(): void {
    const heading = this.document.querySelector<HTMLElement>('#main-content h1');
    if (!heading) {
      return;
    }
    heading.setAttribute('tabindex', '-1'); // focusable by script, not added to the Tab order
    heading.focus();
  }
}
