import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { vi } from 'vitest';
import { authGuard } from './auth-guard';
import { AuthService } from '../../features/auth/auth-service';

describe('authGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  function runGuard() {
    return TestBed.runInInjectionContext(() => authGuard(route, state));
  }

  it('allows navigation when authenticated', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => true } },
        { provide: Router, useValue: { createUrlTree: vi.fn() } },
      ],
    });

    expect(runGuard()).toBe(true);
  });

  it('redirects to /auth/login when not authenticated', () => {
    const urlTree = {} as UrlTree;
    const createUrlTree = vi.fn().mockReturnValue(urlTree);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => false } },
        { provide: Router, useValue: { createUrlTree } },
      ],
    });

    expect(runGuard()).toBe(urlTree);
    expect(createUrlTree).toHaveBeenCalledWith(['/auth/login']);
  });
});
