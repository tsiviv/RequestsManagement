export type RequestStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';

export const REQUEST_STATUSES: readonly RequestStatus[] = [
  'New',
  'InProgress',
  'Waiting',
  'Completed',
];

export type RequestPriority = 'Low' | 'Medium' | 'High';

export const REQUEST_PRIORITIES: readonly RequestPriority[] = ['Low', 'Medium', 'High'];

export const REQUEST_STATUS_LABELS: Record<RequestStatus, string> = {
  New: 'חדש',
  InProgress: 'בטיפול',
  Waiting: 'ממתין',
  Completed: 'הושלם',
};

export const REQUEST_STATUS_ICONS: Record<RequestStatus, string> = {
  New: 'fiber_new',
  InProgress: 'autorenew',
  Waiting: 'hourglass_empty',
  Completed: 'check_circle',
};

export const REQUEST_PRIORITY_LABELS: Record<RequestPriority, string> = {
  Low: 'נמוכה',
  Medium: 'בינונית',
  High: 'גבוהה',
};

export const REQUEST_PRIORITY_ICONS: Record<RequestPriority, string> = {
  Low: 'south',
  Medium: 'remove',
  High: 'north',
};

export interface RequestRecord {
  id: number;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  createdAt: string;
  updatedAt: string;
  /** Base64-encoded optimistic concurrency token; must be echoed back on status updates. */
  rowVersion: string;
}
