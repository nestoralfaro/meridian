import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService, type DemoPersonaKey } from '../../core/auth/auth.service';

interface Persona {
  readonly key: DemoPersonaKey;
  readonly name: string;
  readonly summary: string;
}

@Component({
  selector: 'mer-sign-in-page',
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
