import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'mer-dashboard-page',
  template: `
    <h2>Dashboard</h2>
    <p>Your accounts overview arrives in Sprint 1.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {}
