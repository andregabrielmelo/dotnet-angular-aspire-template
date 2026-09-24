import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AuthService } from '../auth/auth-service';

interface CurrentUser {
  id: number;
  name: string;
  email: string;
}

@Component({
  selector: 'app-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.html',
})
export class HomePage {
  private readonly authService = inject(AuthService);
  private readonly http = inject(HttpClient);

  protected readonly user = this.authService.user;

  // A real API call through the backend for frontend (cookie in, Bearer token out). The first
  // call also creates the user's profile in the application database.
  protected readonly profile = toSignal(
    this.http.get<CurrentUser>('api/users/me').pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  logout(): void {
    this.authService.logout();
  }
}
