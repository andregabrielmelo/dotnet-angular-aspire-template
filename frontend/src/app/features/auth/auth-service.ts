import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { AuthResult } from './models/auth-result.model';
import { AuthenticatedUser } from './models/authenticated-user.model';
import { LoginRequest } from './models/login-request.model';
import { RegisterRequest } from './models/register-request.model';
import { RegisterResponse } from './models/register-response.model';

const REFRESH_TOKEN_KEY = 'apptemplate_refresh_token';

// Requests tagged with this never trigger the auth interceptor's bearer-attach or
// refresh-and-retry logic: they're the auth endpoints themselves (anonymous, or —
// in refresh's case — the refresh mechanism), so reacting to their own 401s would
// either do nothing useful or recurse into another refresh attempt.
export const SKIP_AUTH_HANDLING = new HttpContextToken<boolean>(() => false);

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  // In-memory only — never persisted, so a full page reload always starts from
  // null and relies on bootstrap() to silently refresh it from the stored refresh token.
  readonly accessToken = signal<string | null>(null);
  readonly currentUser = signal<AuthenticatedUser | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>('api/auth/register', request, {
      context: new HttpContext().set(SKIP_AUTH_HANDLING, true),
    });
  }

  login(request: LoginRequest): Observable<AuthResult> {
    return this.http
      .post<AuthResult>('api/auth/login', request, {
        context: new HttpContext().set(SKIP_AUTH_HANDLING, true),
      })
      .pipe(tap((result) => this.setSession(result)));
  }

  /** Reads the stored refresh token and silently mints a new token pair. Resolves
   * `false` (without throwing) if there's no stored token or the server rejects it. */
  refresh(): Observable<boolean> {
    const refreshToken = this.getStoredRefreshToken();
    if (!refreshToken) {
      return of(false);
    }

    return this.http
      .post<AuthResult>(
        'api/auth/refresh',
        { refreshToken },
        { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
      )
      .pipe(
        tap((result) => this.setSession(result)),
        map(() => true),
        catchError(() => {
          this.clearSession();
          return of(false);
        }),
      );
  }

  logout(): Observable<void> {
    const refreshToken = this.getStoredRefreshToken();
    const request$ = refreshToken
      ? this.http.post<void>(
          'api/auth/logout',
          { refreshToken },
          { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
        )
      : of(undefined);

    return request$.pipe(
      catchError(() => of(undefined)),
      tap(() => this.clearSession()),
    );
  }

  confirmEmail(token: string): Observable<void> {
    return this.http.post<void>(
      'api/auth/confirm-email',
      { token },
      { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(
      'api/auth/forgot-password',
      { email },
      { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
    );
  }

  resetPassword(token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(
      'api/auth/reset-password',
      { token, newPassword },
      { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) },
    );
  }

  /** A real top-level navigation, not an HTTP call: Google's consent page has to
   * render as the visible page, and the browser itself needs to follow the final
   * redirect back (an XHR/fetch can't hand that off to the visible document). */
  loginWithGoogle(): void {
    window.location.href = '/api/auth/external/google';
  }

  me(): Observable<AuthenticatedUser> {
    return this.http.get<AuthenticatedUser>('api/auth/me');
  }

  /** Used by the OAuth callback page, which only gets a token pair off the
   * redirect's URL fragment (no `user` object). Rather than decode the JWT's
   * claims client-side — which would couple this code to the backend's exact
   * claim-naming choices — it reuses the same `/auth/me` contract every other
   * authenticated request already relies on. */
  setSessionFromTokens(accessToken: string, refreshToken: string): Observable<AuthenticatedUser> {
    this.accessToken.set(accessToken);
    this.storeRefreshToken(refreshToken);
    return this.me().pipe(tap((user) => this.currentUser.set(user)));
  }

  /** Runs once at app start (see the `provideAppInitializer` call in app.config.ts)
   * so route guards never race an in-flight silent refresh. */
  bootstrap(): Observable<boolean> {
    if (!this.getStoredRefreshToken()) {
      return of(false);
    }
    return this.refresh();
  }

  private setSession(result: AuthResult): void {
    this.accessToken.set(result.accessToken);
    this.currentUser.set(result.user);
    this.storeRefreshToken(result.refreshToken);
  }

  private clearSession(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }

  private getStoredRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private storeRefreshToken(token: string): void {
    localStorage.setItem(REFRESH_TOKEN_KEY, token);
  }
}
