import { RequestPriority, RequestStatus } from './request.model';

export interface RequestSummary {
  total: number;
  byStatus: Record<RequestStatus, number>;
  byPriority: Record<RequestPriority, number>;
}
