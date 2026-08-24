import { RequestPriority, RequestStatus } from './request.model';

export type RequestSortField = 'status' | 'priority' | 'createdAt' | 'updatedAt';

export type RequestSortDirection = 'asc' | 'desc';

export interface RequestQuery {
  search?: string;
  status?: RequestStatus;
  priority?: RequestPriority;
  page: number;
  pageSize: number;
  sortBy?: RequestSortField;
  sortDirection?: RequestSortDirection;
}
