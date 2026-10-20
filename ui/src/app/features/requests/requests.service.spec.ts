import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RequestsService } from './requests.service';
import { environment } from '../../../environments/environment';
import { RequestQuery } from './models/request-query.model';
import { RequestRecord } from './models/request.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { RequestStatusHistoryEntry } from './models/request-history.model';
import { BulkUpdateStatus, BulkUpdateStatusResult } from './models/bulk-update-status.model';

describe('RequestsService', () => {
  let service: RequestsService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/requests`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RequestsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('sends a GET to the requests endpoint with all filters as query parameters', () => {
    const query: RequestQuery = {
      page: 2,
      pageSize: 25,
      search: 'acme',
      status: 'InProgress',
      priority: 'High',
      sortBy: 'createdAt',
      sortDirection: 'desc',
    };

    let result: PagedResult<RequestRecord> | undefined;
    service.getRequests(query).subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');

    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    expect(req.request.params.get('search')).toBe('acme');
    expect(req.request.params.get('status')).toBe('InProgress');
    expect(req.request.params.get('priority')).toBe('High');
    expect(req.request.params.get('sortBy')).toBe('createdAt');
    expect(req.request.params.get('sortDirection')).toBe('desc');

    const response: PagedResult<RequestRecord> = {
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
    };
    req.flush(response);

    expect(result).toEqual(response);
  });

  it('sends the organization, handler and date-range filters', () => {
    service
      .getRequests({
        page: 1,
        pageSize: 20,
        organizationName: 'רכבת ישראל - סניף חיפה',
        assignedTo: 'דנה כהן',
        createdFrom: '2026-07-01',
        createdTo: '2026-08-01',
      })
      .subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('organizationName')).toBe('רכבת ישראל - סניף חיפה');
    expect(req.request.params.get('assignedTo')).toBe('דנה כהן');
    expect(req.request.params.get('createdFrom')).toBe('2026-07-01');
    expect(req.request.params.get('createdTo')).toBe('2026-08-01');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });
  });

  it('requests the status history of a request', () => {
    let result: RequestStatusHistoryEntry[] | undefined;
    service.getHistory(7).subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/7/history`);
    expect(req.request.method).toBe('GET');
    const history: RequestStatusHistoryEntry[] = [
      { id: 1, previousStatus: 'New', newStatus: 'InProgress', changedAt: '2026-08-01T10:00:00Z', changedBy: 'דנה' },
    ];
    req.flush(history);

    expect(result).toEqual(history);
  });

  it('posts a bulk status update with a rowVersion per request', () => {
    const update: BulkUpdateStatus = {
      status: 'Completed',
      items: [
        { id: 1, rowVersion: 'AAAAAAAAAAE=' },
        { id: 2, rowVersion: 'AAAAAAAAAAI=' },
      ],
    };
    let result: BulkUpdateStatusResult | undefined;
    service.bulkUpdateStatus(update).subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/status/bulk`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(update);
    const response: BulkUpdateStatusResult = {
      requested: 2,
      updated: 1,
      failed: 1,
      results: [
        { id: 1, outcome: 'Updated', error: null, request: null },
        { id: 2, outcome: 'Conflict', error: 'modified by another user', request: null },
      ],
    };
    req.flush(response);

    expect(result).toEqual(response);
  });

  it('omits optional filters from the query string when not provided', () => {
    service.getRequests({ page: 1, pageSize: 20 }).subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');

    expect(req.request.params.has('search')).toBe(false);
    expect(req.request.params.has('status')).toBe(false);
    expect(req.request.params.has('priority')).toBe(false);
    expect(req.request.params.has('sortBy')).toBe(false);
    expect(req.request.params.has('sortDirection')).toBe(false);
    expect(req.request.params.has('organizationName')).toBe(false);
    expect(req.request.params.has('assignedTo')).toBe(false);
    expect(req.request.params.has('createdFrom')).toBe(false);
    expect(req.request.params.has('createdTo')).toBe(false);

    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });
  });

  it('requests aggregated data from the summary endpoint', () => {
    service.getSummary().subscribe();

    const req = httpMock.expectOne(`${baseUrl}/summary`);
    expect(req.request.method).toBe('GET');

    req.flush({
      total: 100,
      byStatus: { New: 25, InProgress: 25, Waiting: 25, Completed: 25 },
      byPriority: { Low: 40, Medium: 40, High: 20 },
    });
  });

  it('sends a PATCH with the new status and concurrency token when updating status', () => {
    const updated: RequestRecord = {
      id: 42,
      title: 'Sample',
      organizationName: 'Acme',
      assignedTo: 'Dana',
      status: 'Completed',
      priority: 'Low',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-02T00:00:00Z',
      rowVersion: 'AAAAAAAAB9E=',
    };

    let result: RequestRecord | undefined;
    service
      .updateStatus(42, { status: 'Completed', rowVersion: 'AAAAAAAAB9E=' })
      .subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/42/status`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Completed', rowVersion: 'AAAAAAAAB9E=' });

    req.flush(updated);

    expect(result).toEqual(updated);
  });
});
