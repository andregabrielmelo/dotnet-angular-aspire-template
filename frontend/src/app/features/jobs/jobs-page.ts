import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';
import { RecurringJob } from './jobs.model';
import { JobsService, jobErrorMessage } from './jobs.service';

/**
 * Recurring background jobs. Reaching it requires jobs:read; the action buttons appear only
 * with jobs:manage (the API enforces both regardless).
 */
@Component({
  selector: 'app-jobs-page',
  imports: [RouterLink, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './jobs-page.html',
})
export class JobsPage implements OnInit {
  private readonly jobsService = inject(JobsService);
  private readonly currentUser = inject(CurrentUserService);

  protected readonly jobs = signal<RecurringJob[]>([]);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly canManage = () => this.currentUser.hasPermission(Permission.JobsManage);

  ngOnInit(): void {
    this.load();
  }

  trigger(job: RecurringJob): void {
    this.run(this.jobsService.trigger(job.id), `run ${job.id}`);
  }

  pause(job: RecurringJob): void {
    this.run(this.jobsService.pause(job.id), `pause ${job.id}`);
  }

  resume(job: RecurringJob): void {
    this.run(this.jobsService.resume(job.id), `resume ${job.id}`);
  }

  /** Recoverable with "Restore all jobs" - job definitions live in code. */
  remove(job: RecurringJob): void {
    this.run(this.jobsService.remove(job.id), `remove ${job.id}`);
  }

  restore(): void {
    this.run(this.jobsService.restore(), 'restore the jobs');
  }

  private run(action: Observable<void>, description: string): void {
    this.busy.set(true);
    this.errorMessage.set(null);
    action.subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorMessage.set(jobErrorMessage(error, description));
      },
    });
  }

  private load(): void {
    this.jobsService.list().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.busy.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorMessage.set(jobErrorMessage(error, 'load the jobs'));
      },
    });
  }
}
