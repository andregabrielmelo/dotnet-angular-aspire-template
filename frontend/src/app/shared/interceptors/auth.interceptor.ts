import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService, SKIP_AUTH_HANDLING } from '../../features/auth/auth-service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (req.context.get(SKIP_AUTH_HANDLING)) {
    return next(req);
  }

  const token = authService.accessToken();
  const authorizedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            router.navigateByUrl('/auth/login');
            return throwError(() => error);
          }

          const refreshedToken = authService.accessToken();
          const retriedReq = refreshedToken
            ? req.clone({ setHeaders: { Authorization: `Bearer ${refreshedToken}` } })
            : req;
          return next(retriedReq);
        }),
      );
    }),
  );
};
