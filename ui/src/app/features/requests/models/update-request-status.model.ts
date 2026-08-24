import { RequestStatus } from './request.model';

export interface UpdateRequestStatus {
  status: RequestStatus;
  /** See the `rowVersion` note on {@link RequestRecord}. */
  rowVersion: string;
}
