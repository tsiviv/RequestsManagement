import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { RequestsPageComponent } from './requests-page.component';
import { RequestFiltersComponent } from './components/request-filters/request-filters.component';
import { RequestsTableComponent } from './components/requests-table/requests-table.component';
import { environment } from '../../../environments/environment';
import { RequestRecord } from './models/request.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { RequestSummary } from './models/request-summary.model';
import { httpErrorInterceptor } from '../../core/interceptors/http-error.interceptor';

const baseUrl = `${environment.apiUrl}/requests`;

const SAMPLE_ITEMS: RequestRecord[] = [
  {
    id: 1,
    title: 'First request',
    organizationName: 'Acme Corp',
    status: 'New',
    priority: 'Low',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    rowVersion: 'AAAAAAAAAAE=',
  },
  {
    id: 2,
    title: 'Second request',
    organizationName: 'Globex',
    status: 'InProgress',
    priority: 'High',
    createdAt: '2026-01-02T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    rowVersion: 'AAAAAAAAAAI=',
  },
];

const SAMPLE_PAGED: PagedResult<RequestRecord> = {
  items: SAMPLE_ITEMS,
  totalCount: 2,
  page: 1,
  pageSize: 25,
  totalPages: 1,
};

const SAMPLE_SUMMARY: RequestSummary = {
  total: 2,
  byStatus: { New: 1, InProgress: 1, Waiting: 0, Completed: 0 },
  byPriority: { Low: 1, Medium: 0, High: 1 },
};

function mockMatchMedia(): void {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
}

describe('RequestsPageComponent', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<RequestsPageComponent>>;
  let component: RequestsPageComponent;
  let httpMock: HttpTestingController;

  function flushInitialLoad(
    requestsResponse: PagedResult<RequestRecord> = SAMPLE_PAGED,
    summaryResponse: RequestSummary = SAMPLE_SUMMARY,
  ): void {
    httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET').flush(requestsResponse);
    httpMock.expectOne(`${baseUrl}/summary`).flush(summaryResponse);
    fixture.detectChanges();
  }

  function getFilters(): RequestFiltersComponent {
    return fixture.debugElement.query(By.directive(RequestFiltersComponent))
      .componentInstance as RequestFiltersComponent;
  }

  function getTable(): RequestsTableComponent {
    return fixture.debugElement.query(By.directive(RequestsTableComponent))
      .componentInstance as RequestsTableComponent;
  }

  beforeEach(() => {
    mockMatchMedia();
    TestBed.configureTestingModule({
      imports: [RequestsPageComponent],
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    fixture = TestBed.createComponent(RequestsPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  it('loads requests on initial load', () => {
    flushInitialLoad();

    expect(getTable().items()).toEqual(SAMPLE_ITEMS);
    expect(component.pagedResult()?.totalCount).toBe(2);
  });

  it('debounces search input, sending a single query for the final value', async () => {
    flushInitialLoad();
    vi.useFakeTimers();

    getFilters().form.controls.search.setValue('ac');
    vi.advanceTimersByTime(100);
    getFilters().form.controls.search.setValue('acme');
    vi.advanceTimersByTime(100);
    fixture.detectChanges();

    httpMock.expectNone((r) => r.url === baseUrl && r.method === 'GET');

    vi.advanceTimersByTime(250);
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('search')).toBe('acme');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ ...SAMPLE_PAGED, items: [], totalCount: 0 });
  });

  it('applies the status filter immediately and resets to page 1', async () => {
    flushInitialLoad();

    getFilters().form.controls.status.setValue('InProgress');
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('status')).toBe('InProgress');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(SAMPLE_PAGED);
  });

  it('applies the priority filter immediately and resets to page 1', async () => {
    flushInitialLoad();

    getFilters().form.controls.priority.setValue('High');
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('priority')).toBe('High');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(SAMPLE_PAGED);
  });

  it('requests the correct page and page size when the paginator changes', async () => {
    flushInitialLoad();

    getTable().pageChange.emit({ pageIndex: 2, pageSize: 50, length: 200, previousPageIndex: 0 });
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush({ ...SAMPLE_PAGED, page: 3, pageSize: 50 });
  });

  it('sends the correct sort parameters and resets to page 1 when sorting changes', async () => {
    flushInitialLoad();

    getTable().sortChange.emit({ active: 'title', direction: 'asc' });
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('sortBy')).toBe('title');
    expect(req.request.params.get('sortDirection')).toBe('asc');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(SAMPLE_PAGED);
  });

  it('shows an empty state when there are no results', () => {
    flushInitialLoad({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('אין בקשות זמינות.');
  });

  it('shows a filtered empty-state message with a reset action when filters are active', async () => {
    flushInitialLoad();

    getFilters().form.controls.status.setValue('Completed');
    fixture.detectChanges();
    await fixture.whenStable();

    httpMock
      .expectOne((r) => r.url === baseUrl && r.method === 'GET')
      .flush({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('אין בקשות התואמות את החיפוש והמסננים הנוכחיים.');
  });

  it('shows an error state when loading requests fails', () => {
    httpMock
      .expectOne((r) => r.url === baseUrl && r.method === 'GET')
      .flush('Internal Server Error', { status: 500, statusText: 'Internal Server Error' });
    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('משהו השתבש בשרת. אנא נסה שוב.');
  });

  it('applies a successful status update to the affected row and refreshes the summary', async () => {
    flushInitialLoad();

    const row = SAMPLE_ITEMS[0];
    getTable().statusChange.emit({ row, status: 'InProgress' });

    const patchReq = httpMock.expectOne(`${baseUrl}/${row.id}/status`);
    expect(patchReq.request.method).toBe('PATCH');
    expect(patchReq.request.body).toEqual({ status: 'InProgress', rowVersion: row.rowVersion });

    const updated: RequestRecord = { ...row, status: 'InProgress', rowVersion: 'AAAAAAAAAAM=' };
    patchReq.flush(updated);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.pagedResult()?.items.find((i) => i.id === row.id)).toEqual(updated);
    expect(component.updatingIds().has(row.id)).toBe(false);

    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);
  });

  it('handles a 409 concurrency conflict by reloading the current page without corrupting state', async () => {
    flushInitialLoad();

    const row = SAMPLE_ITEMS[0];
    getTable().statusChange.emit({ row, status: 'Completed' });

    const patchReq = httpMock.expectOne(`${baseUrl}/${row.id}/status`);
    patchReq.flush('Conflict', { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.updatingIds().has(row.id)).toBe(false);
    expect(component.pagedResult()?.items.find((i) => i.id === row.id)).toEqual(row);

    const reloadReq = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    reloadReq.flush(SAMPLE_PAGED);
  });

  it('leaves the row and update state unchanged when a status update fails', async () => {
    flushInitialLoad();

    const row = SAMPLE_ITEMS[1];
    getTable().statusChange.emit({ row, status: 'Waiting' });

    const patchReq = httpMock.expectOne(`${baseUrl}/${row.id}/status`);
    patchReq.flush('Server error', { status: 500, statusText: 'Internal Server Error' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.updatingIds().has(row.id)).toBe(false);
    expect(component.pagedResult()?.items.find((i) => i.id === row.id)).toEqual(row);
    expect(component.error()).toBeNull();
  });
});
