import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { HomePage } from './home-page';
import { AuthService } from '../auth/auth-service';

describe('HomePage', () => {
  it('logs out and navigates to /auth/login', () => {
    const logout = vi.fn().mockReturnValue(of(undefined));
    const navigateByUrl = vi.fn();

    TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        { provide: AuthService, useValue: { currentUser: signal(null), logout } },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });

    const fixture = TestBed.createComponent(HomePage);
    fixture.componentInstance.logout();

    expect(logout).toHaveBeenCalled();
    expect(navigateByUrl).toHaveBeenCalledWith('/auth/login');
  });

  it('shows the current user name reactively', () => {
    TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        {
          provide: AuthService,
          useValue: {
            currentUser: signal({ userId: 1, name: 'Ada Lovelace', email: 'ada@example.com' }),
            logout: vi.fn(),
          },
        },
        { provide: Router, useValue: { navigateByUrl: vi.fn() } },
      ],
    });

    const fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Ada Lovelace');
  });
});
