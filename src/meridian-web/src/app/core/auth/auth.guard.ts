import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { type CanActivateFn, Router } from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

/** Signed-in users only; others go to sign in and come back afterwards. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const status = await firstValueFrom(
    toObservable(auth.status).pipe(filter((s) => s !== 'loading')),
  );
  return status === 'signed-in'
    ? true
    : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};
