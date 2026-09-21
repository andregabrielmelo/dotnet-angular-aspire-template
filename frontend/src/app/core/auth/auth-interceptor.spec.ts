import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from '../../features/auth/auth-service';
import { AuthTokensResponse } from '../../features/auth/models/auth-tokens-response.model';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authServiceMock: { accessToken: string | null; refresh: ReturnType<typeof vi.fn> };
  let routerMock: { navigateByUrl: ReturnType<typeof vi.fn> };

  const newTokens: AuthTokensResponse = {
    userId: 1,
    name: 'Ada Lovelace',
    email: 'ada@example.com',
    accessToken: 'new-access-token',
    accessTokenExpiresAtUtc: new Date().toISOString(),
    refreshToken: 'new-refresh-token',
    refreshTokenExpiresAtUtc: new Date().toISOString(),
  };

  beforeEach(() => {
    authServiceMock = { accessToken: 'old-access-token', refresh: vi.fn() };
    routerMock = { navigateByUrl: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceMock },
        { provide: Router, useValue: routerMock },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches the Authorization header to non-auth requests', () => {
    http.get('api/users').subscribe();

    const req = httpMock.expectOne('api/users');
    expect(req.request.headers.get('Authorization')).toBe('Bearer old-access-token');
    req.flush([]);
  });

  it('does not attach a header to the login/register/refresh endpoints', () => {
    http.post('api/login', {}).subscribe();

    const req = httpMock.expectOne('api/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('refreshes and retries once on a 401, then succeeds', () => {
    authServiceMock.refresh.mockReturnValue(of(newTokens));

    let result: unknown;
    http.get('api/users').subscribe((response) => (result = response));

    const firstAttempt = httpMock.expectOne('api/users');
    firstAttempt.flush(null, { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('api/users');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-access-token');
    retry.flush(['ok']);

    expect(result).toEqual(['ok']);
  });

  it('redirects to login and propagates the error when refresh fails', () => {
    authServiceMock.refresh.mockReturnValue(of(null));

    let error: unknown;
    http.get('api/users').subscribe({ error: (e) => (error = e) });

    httpMock.expectOne('api/users').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(routerMock.navigateByUrl).toHaveBeenCalledWith('/auth/login');
    expect(error).toBeTruthy();
  });
});
