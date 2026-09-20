import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../../features/auth/auth-service';

// Safe to check the signal synchronously: app.config.ts's provideAppInitializer
// awaits AuthService.bootstrap() before the app (and therefore routing) starts,
// so any in-flight silent refresh has already resolved by the time a guard runs.
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.isAuthenticated() ? true : router.createUrlTree(['/auth/login']);
};
