import { RequestPriority, RequestStatus } from './request.model';

export type RequestSortField = 'status' | 'priority' | 'createdAt' | 'updatedAt';

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
