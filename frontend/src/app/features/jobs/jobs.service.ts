import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RecurringJob, RecurringJobDetail } from './jobs.model';

const BASE = 'api/admin/jobs';

/** The recurring-job admin API (reached through the backend for frontend). */
@Injectable({ providedIn: 'root' })
export class JobsService {
  private readonly http = inject(HttpClient);

  list(): Observable<RecurringJob[]> {
    return this.http.get<RecurringJob[]>(BASE);
  }

  get(jobId: string): Observable<RecurringJobDetail> {
    return this.http.get<RecurringJobDetail>(`${BASE}/${encodeURIComponent(jobId)}`);
  }

  trigger(jobId: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${encodeURIComponent(jobId)}/trigger`, null);
  }

  pause(jobId: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${encodeURIComponent(jobId)}/pause`, null);
  }

  resume(jobId: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${encodeURIComponent(jobId)}/resume`, null);
  }

  remove(jobId: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/${encodeURIComponent(jobId)}`);
  }

  restore(): Observable<void> {
    return this.http.post<void>(`${BASE}/restore`, null);
  }
}

/** A user-facing message for a failed job action. */
export function jobErrorMessage(error: HttpErrorResponse, action: string): string {
  switch (error.status) {
    case 403:
      return "You don't have permission to manage jobs.";
    case 404:
      return 'That job no longer exists. Try "Restore all jobs".';
    case 429:
      return 'Too many requests. Please wait a minute and try again.';
    default:
      return `Could not ${action}.`;
  }
}
