import { RequestStatus } from './request.model';

export interface UpdateRequestStatus {
  status: RequestStatus;
  rowVersion: string;
}
