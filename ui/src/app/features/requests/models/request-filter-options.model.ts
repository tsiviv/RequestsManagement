export interface RequestFilterOptions {
  organizations: string[];
  assignees: string[];
}

export const EMPTY_FILTER_OPTIONS: RequestFilterOptions = { organizations: [], assignees: [] };
