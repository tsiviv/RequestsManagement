export type RequestStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';

export const REQUEST_STATUSES: readonly RequestStatus[] = [
  'New',
  'InProgress',
  'Waiting',
  'Completed',
];

export type RequestPriority = 'Low' | 'Medium' | 'High';

export const REQUEST_PRIORITIES: readonly RequestPriority[] = ['Low', 'Medium', 'High'];

export interface RequestRecord {
  id: number;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  createdAt: string;
  updatedAt: string;
  /**
   * Optimistic concurrency token. The backend has no DTO yet — this assumes
   * System.Text.Json's default base64 encoding of the entity's `RowVersion`
   * (SQL Server `rowversion`, mapped `.IsRowVersion()`). Confirm the field
   * name/format once the real API response is available.
   */
  rowVersion: string;
}
