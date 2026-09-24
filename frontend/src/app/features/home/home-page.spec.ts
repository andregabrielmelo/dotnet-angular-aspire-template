import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { HomePage } from './home-page';
import { AuthService } from '../auth/auth-service';

describe('HomePage', () => {
  const logout = vi.fn();

  beforeEach(() => {
    logout.mockClear();
    TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            user: signal({ name: 'Ada Lovelace', email: 'ada@example.com', logoutUrl: '/x' }),
            logout,
          },
        },
      ],
    });
  });

  it('shows the user name and the API profile', async () => {
    const fixture = TestBed.createComponent(HomePage);
    const httpMock = TestBed.inject(HttpTestingController);

    httpMock
      .expectOne('api/users/me')
      .flush({ id: 7, name: 'Ada Lovelace', email: 'ada@example.com' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('Profile #7');
    httpMock.verify();
  });

  it('logs out through the auth service', () => {
    const fixture = TestBed.createComponent(HomePage);
    TestBed.inject(HttpTestingController).expectOne('api/users/me').flush(null);

    fixture.componentInstance.logout();

    expect(logout).toHaveBeenCalled();
  });
});
