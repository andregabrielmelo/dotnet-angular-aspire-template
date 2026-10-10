import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UpdateUserRequest, UpdateUserResponse, UserPage } from './users.model';
import { API_V1 } from '../../core/api/api-paths';

const BASE = `${API_V1}/users`;

/** The users API (reached through the backend for frontend). */
@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  list(page: number, perPage: number): Observable<UserPage> {
    return this.http.get<UserPage>(BASE, { params: { page, per_page: perPage } });
  }

  update(id: number, request: UpdateUserRequest): Observable<UpdateUserResponse> {
    return this.http.put<UpdateUserResponse>(`${BASE}/${id}`, request);
  }

  /** The avatar image, or a 404 when the user has none. Fetched as a blob: the CSRF header can't ride on an <img>. */
  getAvatar(id: number): Observable<Blob> {
    return this.http.get(`${BASE}/${id}/avatar`, { responseType: 'blob' });
  }

  uploadAvatar(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<void>(`${BASE}/${id}/avatar`, form);
  }

  deleteAvatar(id: number): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}/avatar`);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }
}
