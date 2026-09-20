import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';

function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password-page.html',
})
export class ResetPasswordPage {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly token = this.route.snapshot.queryParamMap.get('token');

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly missingToken = signal(!this.token);

  readonly form = this.formBuilder.nonNullable.group(
    {
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

  submit(): void {
    if (this.form.invalid || this.submitting() || !this.token) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.authService.resetPassword(this.token, this.form.getRawValue().password).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigateByUrl('/auth/login?reset=1');
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(extractErrorMessage(error));
      },
    });
  }
}

function extractErrorMessage(error: HttpErrorResponse): string {
  const errors = error.error?.errors as Record<string, string[]> | undefined;
  if (errors) {
    return Object.values(errors).flat().join(' ');
  }
  return 'This reset link is invalid or has expired.';
}
