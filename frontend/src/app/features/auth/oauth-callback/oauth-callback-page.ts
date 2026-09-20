import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './oauth-callback-page.html',
})
export class OAuthCallbackPage {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly failed = signal(false);

  constructor() {
    // Tokens arrive in the URL fragment (never sent to or logged by any server),
    // not the query string — see the backend's ExternalLoginCallbackHandler.
    const fragmentParams = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const accessToken = fragmentParams.get('access_token');
    const refreshToken = fragmentParams.get('refresh_token');

    // Scrub the tokens out of the visible URL/history immediately, whether or
    // not they turn out to be valid.
    history.replaceState(null, '', '/auth/oauth-callback');

    if (!accessToken || !refreshToken) {
      this.failed.set(true);
      return;
    }

    this.authService.setSessionFromTokens(accessToken, refreshToken).subscribe({
      next: () => this.router.navigateByUrl('/home'),
      error: () => this.failed.set(true),
    });
  }
}
