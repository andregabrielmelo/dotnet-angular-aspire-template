import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { RecurringJobDetail } from './jobs.model';
import { JobsService, jobErrorMessage } from './jobs.service';

/** One recurring job with its most recent runs (requires jobs:read). */
@Component({
  selector: 'app-job-detail-page',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './job-detail-page.html',
})
export class JobDetailPage implements OnInit {
  private readonly jobsService = inject(JobsService);

  /** From the :jobId route parameter (component input binding). */
  readonly jobId = input.required<string>();

  protected readonly detail = signal<RecurringJobDetail | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.jobsService.get(this.jobId()).subscribe({
      next: (detail) => this.detail.set(detail),
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(jobErrorMessage(error, 'load the job')),
    });
  }
}
