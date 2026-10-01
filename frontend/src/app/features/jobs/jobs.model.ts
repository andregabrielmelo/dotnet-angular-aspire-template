// Shapes returned by the Web API's /admin/jobs endpoints (camelCase JSON).
export interface RecurringJob {
  id: string;
  cron: string;
  nextExecution: string | null;
  lastExecution: string | null;
  lastStatus: string | null;
  isPaused: boolean;
  createdAt: string | null;
}

export interface JobExecution {
  jobId: string;
  status: string;
  finishedAt: string | null;
  durationMs: number | null;
  error: string | null;
}

export interface RecurringJobDetail {
  job: RecurringJob;
  recentExecutions: JobExecution[];
}
