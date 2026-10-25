import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

type Field = 'displayName' | 'email' | 'password';

@Component({
  selector: 'mer-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-page.html',
  styleUrl: './register-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });

  protected readonly busy = signal(false);
  /** Messages per field, from client-side checks or the server's validation problem. */
  protected readonly errors = signal<Partial<Record<Field | 'form', readonly string[]>>>({});
  protected readonly summary = computed(() => Object.values(this.errors()).flat());

  protected async submit(): Promise<void> {
    const local = this.clientErrors();
    if (Object.keys(local).length > 0) {
      this.showErrors(local);
      return;
    }

    this.busy.set(true);
    const result = await this.auth.register(this.form.getRawValue());
    this.busy.set(false);
    if (result.ok) {
      await this.router.navigateByUrl('/dashboard');
    } else if ('errors' in result) {
      this.showErrors(result.errors);
    } else {
      this.showErrors({ form: [result.message] });
    }
  }

  protected errorsFor(field: Field): readonly string[] {
    return this.errors()[field] ?? [];
  }

  private clientErrors(): Partial<Record<Field, string[]>> {
    const { displayName, email, password } = this.form.controls;
    const errors: Partial<Record<Field, string[]>> = {};
    if (displayName.invalid) {
      errors.displayName = ['Enter your name (up to 100 characters).'];
    }
    if (email.invalid) {
      errors.email = ['Enter an email address, like name@example.com.'];
    }
    if (password.invalid) {
      errors.password = ['Use at least 12 characters.'];
    }
    return errors;
  }

  /** Shows errors, then moves focus to the first invalid field so keyboard and screen-reader users land on it. */
  private showErrors(errors: Partial<Record<string, readonly string[]>>): void {
    this.errors.set(errors);
    const first = (['displayName', 'email', 'password'] as const).find(
      (field) => errors[field]?.length,
    );
    if (first) {
      queueMicrotask(() =>
        this.host.nativeElement.querySelector<HTMLElement>(`#register-${first}`)?.focus(),
      );
    }
  }
}
