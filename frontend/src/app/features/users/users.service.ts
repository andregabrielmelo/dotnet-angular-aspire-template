import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UserPage } from './users.model';

const BASE = 'api/users';

/** The users admin API (reached through the backend for frontend). */
@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  list(page: number, perPage: number): Observable<UserPage> {
    return this.http.get<UserPage>(BASE, { params: { page, per_page: perPage } });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }
}
