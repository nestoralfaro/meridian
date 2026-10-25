import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'mer-dashboard-page',
  template: `
    <h1>Dashboard</h1>
    <p class="greeting">Welcome back, {{ firstName() }}</p>
    <p>Your accounts overview arrives with the next story.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly firstName = inject(AuthService).firstName;
}
