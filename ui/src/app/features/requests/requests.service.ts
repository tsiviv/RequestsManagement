import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result.model';
import { RequestQuery } from './models/request-query.model';
import { RequestRecord } from './models/request.model';
import { RequestSummary } from './models/request-summary.model';
import { UpdateRequestStatus } from './models/update-request-status.model';

/**
 * Talks to the Requests API. The backend has no controllers yet — these
 * methods target the agreed contract (GET /requests, GET /requests/summary,
 * PATCH /requests/{id}/status) and will 404 until the API is implemented.
 */
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

  updateStatus(id: number, update: UpdateRequestStatus): Observable<RequestRecord> {
    return this.http.patch<RequestRecord>(`${this.baseUrl}/${id}/status`, update);
  }

  private toHttpParams(query: RequestQuery): HttpParams {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);

    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.priority) {
      params = params.set('priority', query.priority);
    }
    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return params;
  }
}
