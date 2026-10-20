/** Distinct values for the exact-match filters (GET /requests/filter-options). */
export interface RequestFilterOptions {
  organizations: string[];
  assignees: string[];
}

export const EMPTY_FILTER_OPTIONS: RequestFilterOptions = { organizations: [], assignees: [] };
