import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'mer-landing-page',
  imports: [RouterLink],
  template: `
    <h1>Welcome to Meridian</h1>
    <p>Plan, invest and track your goals in one place.</p>
    <p><a routerLink="/dashboard">Go to your dashboard</a></p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {}
