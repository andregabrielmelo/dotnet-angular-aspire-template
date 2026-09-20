import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';

enum ConfirmationState {
  Confirming = 'confirming',
  Success = 'success',
  Failure = 'failure',
  MissingToken = 'missing-token',
}

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-email-page.html',
})
export class ConfirmEmailPage {
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  protected readonly ConfirmationState = ConfirmationState;

  readonly state = signal<ConfirmationState>(ConfirmationState.Confirming);

  constructor() {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (token === null) {
      this.state.set(ConfirmationState.MissingToken);
      return;
    }

    this.authService.confirmEmail(token).subscribe({
      next: () => this.state.set(ConfirmationState.Success),
      error: () => this.state.set(ConfirmationState.Failure),
    });
  }
}
