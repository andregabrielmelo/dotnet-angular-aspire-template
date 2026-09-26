import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, switchMap, tap } from 'rxjs';
import { RecurringJobDetail } from './jobs.model';
import { JobsService, jobErrorMessage } from './jobs.service';

/** One recurring job with its most recent runs (requires jobs:read). */
@Component({
  selector: 'app-job-detail-page',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './job-detail-page.html',
})
export class JobDetailPage {
  private readonly jobsService = inject(JobsService);

  /** From the :jobId route parameter (component input binding). */
  readonly jobId = input.required<string>();

  protected readonly detail = signal<RecurringJobDetail | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    // The router reuses this component when only :jobId changes (/jobs/a -> /jobs/b), so load
    // on every change of the input. switchMap cancels the previous job's request if it's
    // still in flight, so a slow response can't overwrite the newer job.
    toObservable(this.jobId)
      .pipe(
        tap(() => {
          this.detail.set(null);
          this.errorMessage.set(null);
        }),
        switchMap((jobId) =>
          this.jobsService.get(jobId).pipe(
            catchError((error: HttpErrorResponse) => {
              this.errorMessage.set(jobErrorMessage(error, 'load the job'));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((detail) => this.detail.set(detail));
  }
}
