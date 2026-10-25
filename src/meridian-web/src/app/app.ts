import { ChangeDetectionStrategy, Component, DOCUMENT, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { RouteFocusService } from './core/a11y/route-focus.service';
import { AuthService } from './core/auth/auth.service';
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
  protected readonly shortCommit = inject(BuildInfoService).shortCommit;
  protected readonly auth = inject(AuthService);

  constructor() {
    inject(RouteFocusService).start();
  }

  /** Moves focus to the main region without letting the router treat the fragment as a navigation. */
  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.document.getElementById('main-content')?.focus();
  }
}
