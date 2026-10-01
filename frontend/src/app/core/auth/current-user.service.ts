import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Observable, catchError, of, tap, throwError } from 'rxjs';
import { Permission } from './permissions';

export interface CurrentUser {
  id: number;
  name: string;
  email: string;
  permissions: string[];
}

const CURRENT_USER_URL = 'api/users/me';

/**
 * The signed-in user's application profile and permissions, from GET /api/users/me. On first
 * sign-in that returns 404 - the profile is then created with POST /api/users/me (idempotent,
 * so a second tab doing the same is harmless). Loaded once and cached.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);

  private readonly profileSignal = signal<CurrentUser | null>(null);
  private loaded = false;

  readonly profile = this.profileSignal.asReadonly();
  private readonly permissions = computed(() => new Set(this.profileSignal()?.permissions ?? []));

  load(): Observable<CurrentUser | null> {
    if (this.loaded) {
      return of(this.profileSignal());
    }

    return this.http.get<CurrentUser>(CURRENT_USER_URL).pipe(
      catchError((error: unknown) =>
        error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound
          ? this.http.post<CurrentUser>(CURRENT_USER_URL, null)
          : throwError(() => error),
      ),
      catchError(() => of(null)),
      tap((profile) => {
        this.profileSignal.set(profile);
        this.loaded = profile !== null;
      }),
    );
  }

  hasPermission(permission: Permission): boolean {
    return this.permissions().has(permission);
  }
}
