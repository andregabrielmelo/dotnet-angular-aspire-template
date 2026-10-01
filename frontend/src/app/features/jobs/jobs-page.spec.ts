import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { JobsPage } from './jobs-page';
import { RecurringJob } from './jobs.model';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';

describe('JobsPage', () => {
  const syncJob: RecurringJob = {
    id: 'sync-user-profiles',
    cron: '0 * * * *',
    nextExecution: '2026-09-25T10:00:00Z',
    lastExecution: null,
    lastStatus: null,
    isPaused: false,
    createdAt: null,
  };

  function setup(permissions: string[], jobs: RecurringJob[] = [syncJob]) {
    TestBed.configureTestingModule({
      imports: [JobsPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CurrentUserService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
    const fixture = TestBed.createComponent(JobsPage);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne('api/admin/jobs').flush(jobs);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const button = (label: string) =>
      Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.trim() === label);
    return { fixture, httpMock, element, button };
  }

  it('lists jobs without actions for read-only users', () => {
    const { element } = setup([Permission.JobsRead]);

    expect(element.textContent).toContain('sync-user-profiles');
    expect(element.textContent).toContain('0 * * * *');
    expect(element.querySelector('button')).toBeNull();
  });

  it('pauses a job and reloads the list', () => {
    const { fixture, httpMock, element, button } = setup([
      Permission.JobsRead,
      Permission.JobsManage,
    ]);

    button('Pause')!.click();
    const pause = httpMock.expectOne('api/admin/jobs/sync-user-profiles/pause');
    expect(pause.request.method).toBe('POST');
    pause.flush(null, { status: 204, statusText: 'No Content' });
    httpMock
      .expectOne('api/admin/jobs')
      .flush([{ ...syncJob, isPaused: true, nextExecution: null }]);
    fixture.detectChanges();

    expect(element.textContent).toContain('Paused');
    expect(button('Resume')).toBeTruthy();
  });

  it('runs, removes and restores jobs through the API', () => {
    const { httpMock, button } = setup([Permission.JobsRead, Permission.JobsManage]);

    button('Run now')!.click();
    httpMock.expectOne('api/admin/jobs/sync-user-profiles/trigger').flush(null);
    httpMock.expectOne('api/admin/jobs').flush([syncJob]);

    button('Remove')!.click();
    expect(httpMock.expectOne('api/admin/jobs/sync-user-profiles').request.method).toBe('DELETE');
  });

  it('explains a rate-limited action', () => {
    const { fixture, httpMock, element, button } = setup([
      Permission.JobsRead,
      Permission.JobsManage,
    ]);

    button('Restore all jobs')!.click();
    httpMock
      .expectOne('api/admin/jobs/restore')
      .flush(null, { status: 429, statusText: 'Too Many Requests' });
    fixture.detectChanges();

    expect(element.textContent).toContain('Too many requests');
  });

  it('shows an empty state', () => {
    const { element } = setup([Permission.JobsRead], []);

    expect(element.textContent).toContain('No recurring jobs.');
  });
});
