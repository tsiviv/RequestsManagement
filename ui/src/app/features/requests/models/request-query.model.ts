import { RequestPriority, RequestStatus } from './request.model';

/** Each has a supporting index; status and handler are filters, not sort keys. */
export type RequestSortField = 'createdAt' | 'updatedAt' | 'organizationName' | 'priority' | 'id';

export type RequestSortDirection = 'asc' | 'desc';

export interface RequestQuery {
  search?: string;
  status?: RequestStatus;
  priority?: RequestPriority;
  organizationName?: string;
  assignedTo?: string;
  createdFrom?: string;
  /** Exclusive upper bound on creation date, `YYYY-MM-DD` (the API's range is half-open). */
  createdTo?: string;
  page: number;
  pageSize: number;
  sortBy?: RequestSortField;
  sortDirection?: RequestSortDirection;
}
