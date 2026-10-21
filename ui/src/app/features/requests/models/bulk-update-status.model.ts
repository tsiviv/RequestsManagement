import { RequestRecord, RequestStatus } from './request.model';

export const BULK_UPDATE_MAX_ITEMS = 100;

export interface BulkUpdateStatus {
  status: RequestStatus;
  items: { id: number; rowVersion: string }[];
}

export type BulkUpdateOutcome = 'Updated' | 'NotFound' | 'Conflict' | 'InvalidTransition';

export interface BulkUpdateStatusItemResult {
  id: number;
  outcome: BulkUpdateOutcome;
  error: string | null;
  request: RequestRecord | null;
}

export interface BulkUpdateStatusResult {
  requested: number;
  updated: number;
  failed: number;
  results: BulkUpdateStatusItemResult[];
}
