import { Observable, tap } from 'rxjs';
import { RegisterResponse } from './models/register-response.model';
import { RegisterRequest } from './models/register-request.model';
import { LoginResponse } from './models/login-response.model';
import { LoginRequest } from './models/login-request.model';
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

// No backend token is issued yet (no /login endpoint, and register doesn't return one either),
// so this is a local-only "am I in a session" marker, not real authentication. Swap this out
// once the backend issues a real token to check/store instead.
const SESSION_KEY = 'apptemplate_session_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http
      .post<RegisterResponse>('api/users', request)
      .pipe(tap((response) => this.markSessionActive(response.name)));
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('api/login', request)
      .pipe(tap((response) => this.markSessionActive(response.name)));
  }

  logout(): void {
    localStorage.removeItem(SESSION_KEY);
  }

  isAuthenticated(): boolean {
    return localStorage.getItem(SESSION_KEY) !== null;
  }

  currentUserName(): string | null {
    return localStorage.getItem(SESSION_KEY);
  }

  private markSessionActive(userName: string): void {
    localStorage.setItem(SESSION_KEY, userName);
  }
}
