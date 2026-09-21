import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../../features/auth/auth-service';

// Exact relative paths AuthService posts to - these must never carry a (possibly stale)
// Authorization header, and a 401 from one of them must never trigger a refresh loop.
const UNAUTHENTICATED_ENDPOINTS = ['api/register', 'api/login', 'api/refresh'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isUnauthenticatedEndpoint = UNAUTHENTICATED_ENDPOINTS.includes(request.url);
  const accessToken = authService.accessToken;

  const authorizedRequest =
    !isUnauthenticatedEndpoint && accessToken
      ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;
      if (isUnauthenticatedEndpoint || !isUnauthorized) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            router.navigateByUrl('/auth/login');
            return throwError(() => error);
          }

          const retriedRequest = request.clone({
            setHeaders: { Authorization: `Bearer ${refreshed.accessToken}` },
          });
          return next(retriedRequest);
        }),
      );
    }),
  );
};
