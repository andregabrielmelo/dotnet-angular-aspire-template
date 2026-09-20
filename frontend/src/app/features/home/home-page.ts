import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth-service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.html',
})
export class HomePage {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly userName = this.authService.currentUserName();

  logout(): void {
    this.authService.logout();
    this.router.navigateByUrl('/register');
  }
}
