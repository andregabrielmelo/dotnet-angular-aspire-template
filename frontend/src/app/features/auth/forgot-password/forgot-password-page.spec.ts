import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ForgotPasswordPage } from './forgot-password-page';
import { AuthService } from '../auth-service';

describe('ForgotPasswordPage', () => {
  function setup(requestPasswordReset: ReturnType<typeof vi.fn>) {
    TestBed.configureTestingModule({
      imports: [ForgotPasswordPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: { requestPasswordReset } }],
    });
    const fixture = TestBed.createComponent(ForgotPasswordPage);
    fixture.detectChanges();
    return fixture;
  }

  it('does not submit an invalid email', () => {
    const requestPasswordReset = vi.fn();
    const fixture = setup(requestPasswordReset);

    fixture.componentInstance.form.setValue({ email: 'not-an-email' });
    fixture.componentInstance.submit();

    expect(requestPasswordReset).not.toHaveBeenCalled();
  });

  it('shows the same confirmation once the request is accepted', () => {
    const requestPasswordReset = vi.fn().mockReturnValue(of(undefined));
    const fixture = setup(requestPasswordReset);

    fixture.componentInstance.form.setValue({ email: 'ada@example.com' });
    fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(requestPasswordReset).toHaveBeenCalledWith('ada@example.com');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      "If an account exists for that email, we've sent a link",
    );
  });

  it('explains when the request was rate limited', () => {
    const requestPasswordReset = vi
      .fn()
      .mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
    const fixture = setup(requestPasswordReset);

    fixture.componentInstance.form.setValue({ email: 'ada@example.com' });
    fixture.componentInstance.submit();

    expect(fixture.componentInstance.status()).toBe('idle');
    expect(fixture.componentInstance.errorMessage()).toContain('Too many requests');
  });
});
