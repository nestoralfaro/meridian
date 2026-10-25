import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AccountsService } from '../../core/accounts/accounts.service';
import { AuthService } from '../../core/auth/auth.service';
import { AllocationChart } from '../../shared/ui/allocation-chart';
import {
  describeChange,
  formatChange,
  formatMoney,
  formatPercentChange,
} from '../../shared/ui/money';

@Component({
  selector: 'mer-dashboard-page',
  imports: [AllocationChart],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  protected readonly firstName = inject(AuthService).firstName;
  protected readonly accounts = inject(AccountsService);

  protected readonly money = formatMoney;
  protected readonly change = formatChange;
  protected readonly percent = formatPercentChange;
  protected readonly describe = describeChange;
}
