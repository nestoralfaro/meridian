import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { SessionTimeoutService } from './session-timeout.service';

/** Modal warning before the idle sign-out. Native <dialog> + showModal(): focus is trapped and returned by the browser. */
@Component({
  selector: 'mer-session-timeout-dialog',
  template: `
    <dialog
      #dialog
      class="timeout"
      aria-labelledby="timeout-title"
      aria-describedby="timeout-text"
      (cancel)="$event.preventDefault()"
    >
      <h2 id="timeout-title">Are you still there?</h2>
      <p id="timeout-text">
        For your security, you'll be signed out in
        <span class="timeout__clock">{{ clock() }}</span> unless you choose to stay signed in.
      </p>
      <div class="timeout__actions">
        <button type="button" class="timeout__stay" autofocus (click)="timeout.staySignedIn()">
          Stay signed in
        </button>
        <button type="button" class="timeout__leave" (click)="timeout.signOutNow()">
          Sign out now
        </button>
      </div>
    </dialog>
  `,
  styleUrl: './session-timeout-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionTimeoutDialog {
  protected readonly timeout = inject(SessionTimeoutService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly clock = computed(() => {
    const seconds = this.timeout.secondsLeft();
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  });

  constructor() {
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.timeout.warning() && !dialog.open) {
        dialog.showModal?.();
        dialog.querySelector<HTMLButtonElement>('.timeout__stay')?.focus();
      } else if (!this.timeout.warning() && dialog.open) {
        dialog.close();
      }
    });
  }
}
