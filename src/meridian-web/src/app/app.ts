import { ChangeDetectionStrategy, Component, DOCUMENT, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, of } from 'rxjs';
import { RouteFocusService } from './core/a11y/route-focus.service';
import { BuildInfoService } from './core/build-info/build-info.service';

interface NavItem {
  readonly label: string;
  readonly path: string;
  readonly exact: boolean;
}

@Component({
  selector: 'mer-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly document = inject(DOCUMENT);

  protected readonly navItems: readonly NavItem[] = [
    { label: 'Home', path: '/', exact: true },
    { label: 'Dashboard', path: '/dashboard', exact: false },
  ];

  /** Build info is decorative: if the API is unavailable the footer simply omits it. */
  private readonly buildInfo = toSignal(
    inject(BuildInfoService)
      .get()
      .pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  protected readonly shortCommit = computed(() => this.buildInfo()?.commit.slice(0, 7) ?? null);

  constructor() {
    inject(RouteFocusService).start();
  }

  /** Moves focus to the main region without letting the router treat the fragment as a navigation. */
  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.document.getElementById('main-content')?.focus();
  }
}
