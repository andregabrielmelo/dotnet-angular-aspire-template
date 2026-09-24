import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthPage } from './auth-page';
import { AuthService } from '../auth-service';

describe('AuthPage', () => {
  function setup(user: unknown, providers = [{ alias: 'google', displayName: 'Google' }]) {
    const authService = {
      loadUser: () => of(user),
      loadExternalProviders: () => of(providers),
      login: vi.fn(),
      register: vi.fn(),
      loginWith: vi.fn(),
    };
    const navigateByUrl = vi.fn();
    TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: authService }],
    });
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockImplementation(navigateByUrl);
    const fixture = TestBed.createComponent(AuthPage);
    fixture.detectChanges();
    return { fixture, authService, navigateByUrl };
  }

  it('sends an already signed-in user to /home', () => {
    const { navigateByUrl } = setup({ name: 'Ada', email: null, logoutUrl: '/x' });

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('starts login and registration through the auth service', () => {
    const { fixture, authService, navigateByUrl } = setup(null);

    fixture.componentInstance.login();
    fixture.componentInstance.register();

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(authService.login).toHaveBeenCalled();
    expect(authService.register).toHaveBeenCalled();
  });

  it('offers the enabled third-party providers', () => {
    const { fixture, authService } = setup(null);

    const button = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).find((b) => b.textContent?.includes('Continue with Google'));
    button!.click();

    expect(authService.loginWith).toHaveBeenCalledWith('google');
  });

  it('hides the third-party section when no provider is enabled', () => {
    const { fixture } = setup(null, []);

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Continue with');
  });
});
