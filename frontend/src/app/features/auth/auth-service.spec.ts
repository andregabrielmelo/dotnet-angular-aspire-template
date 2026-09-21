import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth-service';
import { AuthTokensResponse } from './models/auth-tokens-response.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const tokens: AuthTokensResponse = {
    userId: 1,
    name: 'Ada Lovelace',
    email: 'ada@example.com',
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('is not authenticated by default', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken).toBeNull();
  });

  it('register() posts to api/register and stores the session', () => {
    service
      .register({ name: 'Ada Lovelace', email: 'ada@example.com', password: 'Passw0rd!' })
      .subscribe();

    const req = httpMock.expectOne('api/register');
    expect(req.request.method).toBe('POST');
    req.flush(tokens);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.accessToken).toBe('access-token');
    expect(service.currentUserName()).toBe('Ada Lovelace');
  });

  it('login() posts to api/login and stores the session', () => {
    service.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();

    httpMock.expectOne('api/login').flush(tokens);

    expect(service.isAuthenticated()).toBe(true);
  });

  it('refresh() returns null without calling the server when there is no stored refresh token', () => {
    let result: unknown = 'unset';
    service.refresh().subscribe((r) => (result = r));

    expect(result).toBeNull();
    httpMock.expectNone('api/refresh');
  });

  it('logout() clears the session even when the server call fails', () => {
    service.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    httpMock.expectOne('api/login').flush(tokens);

    service.logout().subscribe();
    httpMock
      .expectOne('api/logout')
      .flush(null, { status: 500, statusText: 'Internal Server Error' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken).toBeNull();
    expect(service.currentUserName()).toBeNull();
  });
});
