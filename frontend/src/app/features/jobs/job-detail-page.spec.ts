import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { JobDetailPage } from './job-detail-page';

describe('JobDetailPage', () => {
  function setup() {
    TestBed.configureTestingModule({
      imports: [JobDetailPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(JobDetailPage);
    fixture.componentRef.setInput('jobId', 'sync-user-profiles');
    fixture.detectChanges();
    return {
      fixture,
      httpMock: TestBed.inject(HttpTestingController),
      element: fixture.nativeElement as HTMLElement,
    };
  }

  it('shows the schedule and recent runs, including failures', () => {
    const { fixture, httpMock, element } = setup();

    httpMock.expectOne('api/admin/jobs/sync-user-profiles').flush({
      job: {
        id: 'sync-user-profiles',
        cron: '0 * * * *',
        nextExecution: null,
        lastExecution: '2026-09-25T09:00:00Z',
        lastStatus: 'Failed',
        isPaused: true,
        createdAt: null,
      },
      recentExecutions: [
        {
          jobId: '2',
          status: 'Failed',
          finishedAt: '2026-09-25T09:00:00Z',
          durationMs: null,
          error: 'Keycloak unreachable',
        },
        {
          jobId: '1',
          status: 'Succeeded',
          finishedAt: '2026-09-25T08:00:00Z',
          durationMs: 1234.5,
          error: null,
        },
      ],
    });
    fixture.detectChanges();

    expect(element.textContent).toContain('0 * * * * (paused)');
    expect(element.textContent).toContain('Keycloak unreachable');
    expect(element.textContent).toContain('1,235 ms');
  });

  it('explains a missing job', () => {
    const { fixture, httpMock, element } = setup();

    httpMock
      .expectOne('api/admin/jobs/sync-user-profiles')
      .flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(element.textContent).toContain('That job no longer exists');
  });

  it('loads the new job when the route switches to another job id', async () => {
    const { fixture, httpMock, element } = setup();
    const first = httpMock.expectOne('api/admin/jobs/sync-user-profiles');

    fixture.componentRef.setInput('jobId', 'test-recurring-job');
    fixture.detectChanges();
    await fixture.whenStable();

    // The previous job's request is cancelled, so its late response can't win.
    expect(first.cancelled).toBe(true);
    httpMock.expectOne('api/admin/jobs/test-recurring-job').flush({
      job: {
        id: 'test-recurring-job',
        cron: '0 0 * * *',
        nextExecution: null,
        lastExecution: null,
        lastStatus: null,
        isPaused: false,
        createdAt: null,
      },
      recentExecutions: [],
    });
    fixture.detectChanges();

    expect(element.textContent).toContain('0 0 * * *');
    expect(element.textContent).toContain('No runs yet.');
  });
});
