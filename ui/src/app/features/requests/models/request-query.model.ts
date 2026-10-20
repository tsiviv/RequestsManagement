import { RequestPriority, RequestStatus } from './request.model';

/**
 * Fields the API can sort by: each has a supporting index. Organization sorts by creation date
 * second (same direction). Status, priority and handler are filters, not sort keys.
 */
export type RequestSortField = 'createdAt' | 'updatedAt' | 'organizationName' | 'id';

export type RequestSortDirection = 'asc' | 'desc';

export interface RequestQuery {
  /** Partial match on title or organization name. */
  search?: string;
  status?: RequestStatus;
  priority?: RequestPriority;
  /** Exact organization name. */
  organizationName?: string;
  /** Exact handler name. */
  assignedTo?: string;
  /** Inclusive lower bound on creation date, `YYYY-MM-DD`. */
  createdFrom?: string;
  /** Exclusive upper bound on creation date, `YYYY-MM-DD` (the API's range is half-open). */
  createdTo?: string;
  page: number;
  pageSize: number;
  sortBy?: RequestSortField;
  sortDirection?: RequestSortDirection;
}
