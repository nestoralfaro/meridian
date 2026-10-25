import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ActivityList } from '../../shared/ui/activity-list';

@Component({
  selector: 'mer-activity-page',
  imports: [ActivityList],
  template: `
    <h1>Activity</h1>
    <p class="intro">Your 50 most recent transactions across all accounts, newest first.</p>
    <mer-activity-list [limit]="50" />
  `,
  styles: `
    .intro {
      color: var(--color-text-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivityPage {}
