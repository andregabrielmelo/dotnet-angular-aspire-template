import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password-page.html',
})
export class ForgotPasswordPage {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  readonly submitting = signal(false);
  // Always shown after a submit attempt, regardless of whether the email is
  // actually registered — the backend returns 204 either way to avoid leaking
  // which emails have accounts.
  readonly submitted = signal(false);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    this.authService.forgotPassword(this.form.getRawValue().email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitted.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.submitted.set(true);
      },
    });
  }
}
