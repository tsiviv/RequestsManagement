import { RequestStatus } from './request.model';

export interface RequestStatusHistoryEntry {
  id: number;
  previousStatus: RequestStatus;
  newStatus: RequestStatus;
  changedAt: string;
  changedBy: string;
}
