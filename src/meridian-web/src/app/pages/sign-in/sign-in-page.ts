import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService, type DemoPersonaKey } from '../../core/auth/auth.service';

interface Persona {
  readonly key: DemoPersonaKey;
  readonly name: string;
  readonly summary: string;
}

@Component({
  selector: 'mer-sign-in-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './sign-in-page.html',
  styleUrl: './sign-in-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly personas: readonly Persona[] = [
    {
      key: 'dana',
      name: 'Dana',
      summary: 'New investor building a brokerage account and a Roth IRA.',
    },
    { key: 'raj', name: 'Raj', summary: 'Pre-retiree with brokerage, IRA and savings accounts.' },
  ];
  protected readonly busy = signal(false);
  protected readonly failed = signal(false);

  protected readonly credentials = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly credentialError = signal<string | null>(null);

  protected async signInWithPassword(): Promise<void> {
    if (this.credentials.invalid) {
      this.credentialError.set('Enter your email and password.');
      return;
    }
    this.busy.set(true);
    this.credentialError.set(null);
    const result = await this.auth.signIn(this.credentials.getRawValue());
    this.busy.set(false);
    if (result.ok) {
      await this.router.navigateByUrl(this.returnUrl());
    } else {
      this.credentialError.set(
        'message' in result ? result.message : 'Sign-in failed. Please try again.',
      );
    }
  }

  protected async signInAs(persona: DemoPersonaKey): Promise<void> {
    this.busy.set(true);
    this.failed.set(false);
    try {
      await this.auth.demoSignIn(persona);
      await this.router.navigateByUrl(this.returnUrl());
    } catch {
      this.failed.set(true);
    } finally {
      this.busy.set(false);
    }
  }

  /** Only same-site paths: an open redirect would let a crafted link send users to a look-alike site. */
  private returnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');
    return requested?.startsWith('/') && !requested.startsWith('//') ? requested : '/dashboard';
  }
}
