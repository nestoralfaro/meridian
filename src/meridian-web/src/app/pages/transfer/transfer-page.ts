import {
  DOCUMENT,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { AccountSummaryResponse, TransferResponse } from '../../core/api';
import { AccountsService } from '../../core/accounts/accounts.service';
import { TransferService } from '../../core/transfers/transfer.service';
import { formatMoney } from '../../shared/ui/money';

type Step = 'edit' | 'review' | 'done';
type Field = 'fromAccountId' | 'toAccountId' | 'amount';

/** What the client confirmed: the source account's version and the idempotency key are fixed at review time. */
interface Review {
  readonly from: AccountSummaryResponse;
  readonly to: AccountSummaryResponse;
  readonly amount: number;
  readonly idempotencyKey: string;
}

/** Whole cents, no sign, optional thousands separators: "500", "1,250.5", "0.99". */
const AMOUNT_PATTERN = /^\d{1,3}(,?\d{3})*(\.\d{1,2})?$/;

/** Move cash between your own accounts: edit → review → confirm, with an idempotency key per reviewed transfer. */
@Component({
  selector: 'mer-transfer-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './transfer-page.html',
  styleUrl: './transfer-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TransferPage {
  protected readonly accounts = inject(AccountsService);
  private readonly transfers = inject(TransferService);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  private readonly stepHeading = viewChild<ElementRef<HTMLElement>>('stepHeading');

  protected readonly form = inject(NonNullableFormBuilder).group({
    fromAccountId: [''],
    toAccountId: [''],
    amount: [''],
  });

  protected readonly step = signal<Step>('edit');
  protected readonly review = signal<Review | null>(null);
  protected readonly completed = signal<TransferResponse | null>(null);
  protected readonly busy = signal(false);
  protected readonly errors = signal<Partial<Record<Field | 'form', readonly string[]>>>({});
  protected readonly summary = computed(() => Object.values(this.errors()).flat());
  /** Shown on the review step after a failed attempt (e.g. a timeout); retrying reuses the same key. */
  protected readonly retryMessage = signal<{ message: string; referenceId: string | null } | null>(
    null,
  );

  protected readonly ownAccounts = computed(() => {
    const state = this.accounts.state();
    return state.status === 'loaded' ? state.overview.accounts : [];
  });

  protected readonly money = formatMoney;

  protected errorsFor(field: Field): readonly string[] {
    return this.errors()[field] ?? [];
  }

  protected toReview(): void {
    const { fromAccountId, toAccountId, amount } = this.form.getRawValue();
    const accounts = this.ownAccounts();
    const from = accounts.find((a) => a.id === fromAccountId);
    const to = accounts.find((a) => a.id === toAccountId);
    const errors: Partial<Record<Field, string[]>> = {};

    if (!from) {
      errors.fromAccountId = ['Choose the account to move money from.'];
    }
    if (!to) {
      errors.toAccountId = ['Choose the account to move money to.'];
    } else if (to.id === from?.id) {
      errors.toAccountId = ['Choose a different account to move money to.'];
    }
    const value = parseAmount(amount);
    if (value === null || value <= 0) {
      errors.amount = ['Enter an amount in dollars and cents, like 250.00.'];
    } else if (from && value > from.cash.amount) {
      errors.amount = [`You have ${formatMoney(from.cash)} available in ${from.name}.`];
    }

    if (Object.keys(errors).length > 0 || !from || !to || value === null) {
      this.showErrors(errors);
      return;
    }
    this.errors.set({});
    this.retryMessage.set(null);
    this.review.set({ from, to, amount: value, idempotencyKey: crypto.randomUUID() });
    this.goTo('review');
  }

  protected edit(): void {
    this.retryMessage.set(null);
    this.goTo('edit');
  }

  protected async confirm(): Promise<void> {
    const review = this.review();
    if (!review || this.busy()) {
      return;
    }
    this.busy.set(true);
    const result = await this.transfers.transfer(
      {
        fromAccountId: review.from.id,
        toAccountId: review.to.id,
        amount: review.amount,
        fromAccountVersion: review.from.version,
      },
      review.idempotencyKey,
    );
    this.busy.set(false);

    switch (result.kind) {
      case 'completed':
        this.completed.set(result.transfer);
        this.accounts.reload();
        this.form.reset();
        this.goTo('done');
        return;
      case 'failed':
        this.retryMessage.set({ message: result.message, referenceId: result.referenceId });
        this.focusStepHeading();
        return;
      case 'changed':
        // Balances moved underneath us: show fresh numbers and make the client look again.
        this.accounts.reload();
        this.showErrors({ form: [result.message] }, 'edit');
        return;
      case 'insufficient-cash':
        this.accounts.reload();
        this.showErrors({ amount: [result.message] }, 'edit');
        return;
      case 'invalid':
        this.showErrors(result.errors, 'edit');
        return;
    }
  }

  protected another(): void {
    this.completed.set(null);
    this.review.set(null);
    this.goTo('edit');
  }

  private goTo(step: Step): void {
    this.step.set(step);
    this.focusStepHeading();
  }

  private focusStepHeading(): void {
    afterNextRender(() => this.stepHeading()?.nativeElement.focus(), { injector: this.injector });
  }

  /** Shows errors (on the edit step) and moves focus to the first invalid field, or the summary. */
  private showErrors(
    errors: Partial<Record<string, readonly string[]>>,
    step: Step = this.step(),
  ): void {
    this.errors.set(errors);
    this.step.set(step);
    const first = (['fromAccountId', 'toAccountId', 'amount'] as const).find(
      (f) => errors[f]?.length,
    );
    afterNextRender(
      () => {
        const target = first
          ? this.document.getElementById(`transfer-${first}`)
          : this.document.getElementById('transfer-error-summary');
        target?.focus();
      },
      { injector: this.injector },
    );
  }
}

/** Parses "1,250.50" → 1250.5; null when it isn't a plain amount in whole cents. */
export function parseAmount(text: string): number | null {
  const trimmed = text.trim().replace(/^\$/, '');
  if (!AMOUNT_PATTERN.test(trimmed)) {
    return null;
  }
  return Number(trimmed.replaceAll(',', ''));
}
