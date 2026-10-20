import { RequestRecord, RequestStatus } from './request.model';

/** Server-side limit on items per bulk call (BulkUpdateStatusDto.MaxItems). */
export const BULK_UPDATE_MAX_ITEMS = 100;

export interface BulkUpdateStatus {
  status: RequestStatus;
  /** Each item carries the rowVersion the client last saw, so every request is concurrency-checked on its own. */
  items: { id: number; rowVersion: string }[];
}

export type BulkUpdateOutcome = 'Updated' | 'NotFound' | 'Conflict' | 'InvalidTransition';

export interface BulkUpdateStatusItemResult {
  id: number;
  outcome: BulkUpdateOutcome;
  /** Why the item failed; null when updated. */
  error: string | null;
  /** The updated request (with its new rowVersion); null unless updated. */
  request: RequestRecord | null;
}

/** Partial success: 200 with a per-request outcome, in input order. */
export interface BulkUpdateStatusResult {
  requested: number;
  updated: number;
  failed: number;
  results: BulkUpdateStatusItemResult[];
}
