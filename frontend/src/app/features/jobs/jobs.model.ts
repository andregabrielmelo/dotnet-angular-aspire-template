import { ApiSchema } from '../../core/api/api-schema';

// Shapes returned by the Web API's /v1/admin/jobs endpoints, generated from its OpenAPI document.
export type RecurringJob = ApiSchema<'RecurringJobResponse'>;
export type JobExecution = ApiSchema<'JobExecutionResponse'>;
export type RecurringJobDetail = ApiSchema<'RecurringJobDetailResponse'>;
