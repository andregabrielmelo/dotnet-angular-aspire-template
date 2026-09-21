import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';
import { AuthTokensResponse } from './models/auth-tokens-response.model';
import { LoginRequest } from './models/login-request.model';
import { LoginResponse } from './models/login-response.model';
import { LogoutRequest } from './models/logout-request.model';
import { RefreshRequest } from './models/refresh-request.model';
import { RefreshResponse } from './models/refresh-response.model';
import { RegisterRequest } from './models/register-request.model';
import { RegisterResponse } from './models/register-response.model';

const ACCESS_TOKEN_KEY = 'apptemplate_access_token';
const ACCESS_TOKEN_EXPIRES_AT_KEY = 'apptemplate_access_token_expires_at';
const REFRESH_TOKEN_KEY = 'apptemplate_refresh_token';
const USER_KEY = 'apptemplate_user';

export interface AuthenticatedUser {
  userId: number;
  name: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessTokenSignal = signal<string | null>(
    localStorage.getItem(ACCESS_TOKEN_KEY),
  );
  private readonly currentUserSignal = signal<AuthenticatedUser | null>(readStoredUser());

  readonly currentUser = this.currentUserSignal.asReadonly();

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http
      .post<RegisterResponse>('api/register', request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('api/login', request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /** Returns the new tokens, or null if there was nothing to refresh or the refresh failed. */
  refresh(): Observable<RefreshResponse | null> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) {
      return of(null);
    }

    return this.http
      .post<RefreshResponse>('api/refresh', { refreshToken } satisfies RefreshRequest)
      .pipe(
        tap((response) => this.storeSession(response)),
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
      );
  }

  /** Best-effort: the local session is always cleared, even if the server call fails. */
  logout(): Observable<void> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) {
      this.clearSession();
      return of(undefined);
    }

    return this.http.post<void>('api/logout', { refreshToken } satisfies LogoutRequest).pipe(
      catchError(() => of(undefined)),
      tap(() => this.clearSession()),
    );
  }

  isAuthenticated(): boolean {
    const expiresAt = localStorage.getItem(ACCESS_TOKEN_EXPIRES_AT_KEY);
    return this.accessTokenSignal() !== null && !!expiresAt && new Date(expiresAt) > new Date();
  }

  currentUserName(): string | null {
    return this.currentUserSignal()?.name ?? null;
  }

  private storeSession(tokens: AuthTokensResponse): void {
    const user: AuthenticatedUser = {
      userId: tokens.userId,
      name: tokens.name,
      email: tokens.email,
    };

    localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
    localStorage.setItem(ACCESS_TOKEN_EXPIRES_AT_KEY, tokens.accessTokenExpiresAtUtc);
    localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(user));

    this.accessTokenSignal.set(tokens.accessToken);
    this.currentUserSignal.set(user);
  }

  private clearSession(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(ACCESS_TOKEN_EXPIRES_AT_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);

    this.accessTokenSignal.set(null);
    this.currentUserSignal.set(null);
  }
}

function readStoredUser(): AuthenticatedUser | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AuthenticatedUser;
  } catch {
    return null;
  }
}
