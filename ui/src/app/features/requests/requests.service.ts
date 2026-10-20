import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result.model';
import { RequestQuery } from './models/request-query.model';
import { RequestRecord } from './models/request.model';
import { RequestSummary } from './models/request-summary.model';
import { UpdateRequestStatus } from './models/update-request-status.model';
import { RequestStatusHistoryEntry } from './models/request-history.model';
import { RequestFilterOptions } from './models/request-filter-options.model';
import { BulkUpdateStatus, BulkUpdateStatusResult } from './models/bulk-update-status.model';

/** Optional filters forwarded as query parameters only when set. */
const OPTIONAL_QUERY_PARAMS = [
  'search',
  'status',
  'priority',
  'organizationName',
  'assignedTo',
  'createdFrom',
  'createdTo',
  'sortBy',
  'sortDirection',
] as const satisfies readonly (keyof RequestQuery)[];

@Injectable({ providedIn: 'root' })
export class RequestsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/requests`;

  getRequests(query: RequestQuery): Observable<PagedResult<RequestRecord>> {
    return this.http.get<PagedResult<RequestRecord>>(this.baseUrl, {
      params: this.toHttpParams(query),
    });
  }

  getSummary(): Observable<RequestSummary> {
    return this.http.get<RequestSummary>(`${this.baseUrl}/summary`);
  }

  getFilterOptions(): Observable<RequestFilterOptions> {
    return this.http.get<RequestFilterOptions>(`${this.baseUrl}/filter-options`);
  }

  getHistory(id: number): Observable<RequestStatusHistoryEntry[]> {
    return this.http.get<RequestStatusHistoryEntry[]>(`${this.baseUrl}/${id}/history`);
  }

  updateStatus(id: number, update: UpdateRequestStatus): Observable<RequestRecord> {
    return this.http.patch<RequestRecord>(`${this.baseUrl}/${id}/status`, update);
  }

  bulkUpdateStatus(update: BulkUpdateStatus): Observable<BulkUpdateStatusResult> {
    return this.http.post<BulkUpdateStatusResult>(`${this.baseUrl}/status/bulk`, update);
  }

  private toHttpParams(query: RequestQuery): HttpParams {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    for (const key of OPTIONAL_QUERY_PARAMS) {
      const value = query[key];
      if (value) {
        params = params.set(key, value);
      }
    }

    return params;
  }
}
