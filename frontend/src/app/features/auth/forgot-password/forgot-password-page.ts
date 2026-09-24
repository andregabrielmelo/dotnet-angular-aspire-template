import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';

type Status = 'idle' | 'submitting' | 'sent';

/**
 * Starts a password reset. Keycloak emails a link to its own "set a new password" page and,
 * once the new password is saved, sends the user back to /auth to sign in.
 */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password-page.html',
})
export class ForgotPasswordPage {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  readonly status = signal<Status>('idle');
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  submit(): void {
    if (this.form.invalid || this.status() === 'submitting') {
      this.form.markAllAsTouched();
      return;
    }

    this.status.set('submitting');
    this.errorMessage.set(null);

    this.authService.requestPasswordReset(this.form.getRawValue().email.trim()).subscribe({
      next: () => this.status.set('sent'),
      error: (error: HttpErrorResponse) => {
        this.status.set('idle');
        this.errorMessage.set(
          error.status === 429
            ? 'Too many requests. Please wait a minute and try again.'
            : 'We could not send the email right now. Please try again later.',
        );
      },
    });
  }
}
