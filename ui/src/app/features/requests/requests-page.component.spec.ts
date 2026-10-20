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
import { userNameInterceptor } from '../../core/interceptors/user-name.interceptor';
import { CurrentUserService } from '../../core/user/current-user.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { BulkActionBarComponent } from './components/bulk-action-bar/bulk-action-bar.component';
import { RequestHistoryDialogComponent } from './components/request-history-dialog/request-history-dialog.component';

const baseUrl = `${environment.apiUrl}/requests`;

const SAMPLE_ITEMS: RequestRecord[] = [
  {
    id: 1,
    title: 'First request',
    organizationName: 'Acme Corp',
    assignedTo: 'Dana',
    status: 'New',
    priority: 'Low',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    allowedNextStatuses: ['InProgress'],
    rowVersion: 'AAAAAAAAAAE=',
  },
  {
    id: 2,
    title: 'Second request',
    organizationName: 'Globex',
    assignedTo: 'Yossi',
    status: 'InProgress',
    priority: 'High',
    createdAt: '2026-01-02T00:00:00Z',
    updatedAt: '2026-01-02T00:00:00Z',
    allowedNextStatuses: ['Waiting', 'Completed'],
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

const SAMPLE_FILTER_OPTIONS = {
  organizations: ['Acme Corp', 'Globex'],
  assignees: ['Dana', 'Yossi'],
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
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [RequestsPageComponent],
      providers: [
        provideHttpClient(withInterceptors([userNameInterceptor, httpErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    // Status updates require a name (recorded as ChangedBy); individual tests may clear it.
    TestBed.inject(CurrentUserService).setUserName('דנה כהן');

    fixture = TestBed.createComponent(RequestsPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    httpMock.expectOne(`${baseUrl}/filter-options`).flush(SAMPLE_FILTER_OPTIONS);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
    localStorage.clear();
  });

  it('loads requests on initial load', () => {
    flushInitialLoad();

    expect(getTable().items()).toEqual(SAMPLE_ITEMS);
    expect(component.pagedResult()?.totalCount).toBe(2);
  });

  it('cancels a still-running request when the query changes, so a stale response can never win', async () => {
    const initial = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);

    getFilters().form.controls.status.setValue('Waiting');
    fixture.detectChanges();
    await fixture.whenStable();

    // switchMap unsubscribed from the first request, which aborts it.
    expect(initial.cancelled).toBe(true);
    const replacement = httpMock.expectOne((r) => r.url === baseUrl && r.params.get('status') === 'Waiting');
    replacement.flush(SAMPLE_PAGED);
    fixture.detectChanges();
    expect(component.pagedResult()).toEqual(SAMPLE_PAGED);
  });

  it('shows a loading state first, then keeps the current rows visible while refreshing', async () => {
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text()).toContain('טוען בקשות');

    flushInitialLoad();
    expect(text()).not.toContain('טוען בקשות');

    getTable().sortChange.emit({ active: 'updatedAt', direction: 'asc' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    expect(getTable().items()).toEqual(SAMPLE_ITEMS); // not blanked out while the next page loads
    httpMock.expectOne((r) => r.url === baseUrl && r.params.get('sortBy') === 'updatedAt').flush(SAMPLE_PAGED);
    fixture.detectChanges();
    expect(element.querySelector('mat-progress-bar')).toBeNull();
  });

  it('displays the aggregations: total and counts per status and priority', () => {
    flushInitialLoad();

    const element = fixture.nativeElement as HTMLElement;
    const statusTiles = Object.fromEntries(
      Array.from(element.querySelectorAll('.summary__tile:not(.summary__tile--priority)')).map((tile) => [
        tile.querySelector('.summary__label')?.textContent?.trim(),
        tile.querySelector('.summary__value')?.textContent?.trim(),
      ]),
    );
    expect(statusTiles).toEqual({ 'סה״כ בקשות': '2', חדש: '1', בטיפול: '1', ממתין: '0', הושלם: '0' });

    const byPriority = Object.fromEntries(
      Array.from(element.querySelectorAll('.summary__priority-item')).map((item) => [
        item.querySelector('.summary__priority-name')?.textContent?.trim(),
        item.querySelector('.summary__priority-count')?.textContent?.trim(),
      ]),
    );
    expect(byPriority).toEqual({ נמוכה: '1', בינונית: '0', גבוהה: '1' });
  });

  it('shows a summary error with a retry, independently of the list', () => {
    httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET').flush(SAMPLE_PAGED);
    httpMock.expectOne(`${baseUrl}/summary`).flush('down', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('לא ניתן לטעון את הסיכום');
    expect(getTable().items()).toEqual(SAMPLE_ITEMS);

    const retry = Array.from(element.querySelectorAll('.summary button')).find((b) => b.textContent?.includes('נסה שוב')) as HTMLButtonElement;
    retry.click();
    fixture.detectChanges();
    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);
    fixture.detectChanges();
    expect(element.textContent).not.toContain('לא ניתן לטעון את הסיכום');
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

  it('offers sorting only on the columns the API can sort by', () => {
    flushInitialLoad();

    const sortable = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('th.mat-sort-header')).map((th) =>
      th.textContent?.trim(),
    );
    // Id, organization, priority, created, updated; status and handler are filters, not sort keys.
    expect(sortable).toEqual(['מזהה', 'ארגון', 'עדיפות', 'נוצר', 'עודכן']);
  });

  it('sorts by priority most urgent first on the first click of its header', async () => {
    flushInitialLoad();

    const priorityHeader = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('th.mat-sort-header')).find(
      (th) => th.textContent?.trim() === 'עדיפות',
    )!;
    priorityHeader.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('sortBy')).toBe('priority');
    expect(req.request.params.get('sortDirection')).toBe('desc');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(SAMPLE_PAGED);
  });

  it('shows the handler of each request in its own column', () => {
    flushInitialLoad();

    const table = fixture.nativeElement.querySelector('table') as HTMLTableElement;
    const headers = Array.from(table.querySelectorAll('th')).map((th) => th.textContent?.trim());
    const handlerColumn = headers.indexOf('מטפל');
    expect(handlerColumn).toBeGreaterThan(headers.indexOf('ארגון'));

    const handlers = Array.from(table.querySelectorAll('tbody tr')).map(
      (tr) => tr.querySelectorAll('td')[handlerColumn]?.textContent?.trim(),
    );
    expect(handlers).toEqual(['Dana', 'Yossi']);
  });

  it('opens the change history of the row whose history button was clicked', () => {
    flushInitialLoad();
    const dialogOpen = vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({} as never);

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.row-actions__history');
    buttons[1].click();

    expect(dialogOpen).toHaveBeenCalledWith(
      RequestHistoryDialogComponent,
      expect.objectContaining({ data: { requestId: SAMPLE_ITEMS[1].id, requestTitle: SAMPLE_ITEMS[1].title } }),
    );
  });

  it('offers the organization and handler choices loaded from the API', () => {
    flushInitialLoad();

    expect(getFilters().organizations()).toEqual(SAMPLE_FILTER_OPTIONS.organizations);
    expect(getFilters().assignees()).toEqual(SAMPLE_FILTER_OPTIONS.assignees);
  });

  it('applies organization and handler filters immediately (exact match) and resets to page 1', async () => {
    flushInitialLoad();
    getTable().pageChange.emit({ pageIndex: 2, pageSize: 25, length: 200, previousPageIndex: 0 });
    fixture.detectChanges();
    await fixture.whenStable();
    httpMock.expectOne((r) => r.url === baseUrl && r.params.get('page') === '3').flush(SAMPLE_PAGED);

    getFilters().form.controls.organizationName.setValue('Globex');
    getFilters().form.controls.assignedTo.setValue('Yossi');
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.params.get('assignedTo') === 'Yossi');
    expect(req.request.params.get('organizationName')).toBe('Globex');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(SAMPLE_PAGED);
  });

  it('sends a picked date range as a half-open range: the end day is included', async () => {
    flushInitialLoad();

    getFilters().form.controls.created.setValue({ start: new Date(2026, 6, 1), end: new Date(2026, 6, 31) });
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.params.has('createdTo'));
    expect(req.request.params.get('createdFrom')).toBe('2026-07-01');
    // The API excludes createdTo, so the day after the picked end date is sent.
    expect(req.request.params.get('createdTo')).toBe('2026-08-01');
    req.flush(SAMPLE_PAGED);
  });

  it('reset clears the new filters too', async () => {
    flushInitialLoad();
    getFilters().form.controls.assignedTo.setValue('Dana');
    fixture.detectChanges();
    await fixture.whenStable();
    httpMock.expectOne((r) => r.url === baseUrl && r.params.has('assignedTo')).flush(SAMPLE_PAGED);

    getFilters().resetFilters();
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    for (const param of ['assignedTo', 'organizationName', 'createdFrom', 'createdTo']) {
      expect(req.request.params.has(param)).toBe(false);
    }
    req.flush(SAMPLE_PAGED);
  });

  it('still works when the filter choices cannot be loaded', () => {
    flushInitialLoad();
    const second = TestBed.createComponent(RequestsPageComponent);
    httpMock.expectOne(`${baseUrl}/filter-options`).flush('down', { status: 500, statusText: 'Server Error' });
    second.detectChanges();
    httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET').flush(SAMPLE_PAGED);
    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);
    second.detectChanges();

    const filters = second.debugElement.query(By.directive(RequestFiltersComponent)).componentInstance as RequestFiltersComponent;
    expect(filters.organizations()).toEqual([]);
    expect(second.componentInstance.pagedResult()).toEqual(SAMPLE_PAGED);
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

    getTable().sortChange.emit({ active: 'organizationName', direction: 'asc' });
    fixture.detectChanges();
    await fixture.whenStable();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET');
    expect(req.request.params.get('sortBy')).toBe('organizationName');
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
    expect(patchReq.request.headers.get('X-User-Name')).toBe(encodeURIComponent('דנה כהן'));

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

  // --- Bulk update ---------------------------------------------------------------

  function getBulkBar(): BulkActionBarComponent | null {
    return (fixture.debugElement.query(By.directive(BulkActionBarComponent))?.componentInstance as BulkActionBarComponent) ?? null;
  }

  function selectBothRows(): void {
    getTable().rowSelectionToggle.emit(SAMPLE_ITEMS[0]);
    getTable().rowSelectionToggle.emit(SAMPLE_ITEMS[1]);
    fixture.detectChanges();
  }

  it('shows the bulk bar only while rows are selected, offering the union of their allowed statuses', () => {
    flushInitialLoad();
    expect(getBulkBar()).toBeNull();

    selectBothRows();

    expect(getBulkBar()!.selectedCount()).toBe(2);
    // New -> [InProgress] and InProgress -> [Waiting, Completed], in status order.
    expect(getBulkBar()!.targetStatuses()).toEqual(['InProgress', 'Waiting', 'Completed']);

    getTable().rowSelectionToggle.emit(SAMPLE_ITEMS[1]);
    fixture.detectChanges();
    expect(getBulkBar()!.targetStatuses()).toEqual(['InProgress']);
  });

  it('sends every selected request with its own rowVersion and applies a partial success', async () => {
    flushInitialLoad();
    selectBothRows();

    getBulkBar()!.apply.emit('Completed');

    const req = httpMock.expectOne(`${baseUrl}/status/bulk`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-User-Name')).toBe(encodeURIComponent('דנה כהן'));
    expect(req.request.body).toEqual({
      status: 'Completed',
      items: [
        { id: 1, rowVersion: SAMPLE_ITEMS[0].rowVersion },
        { id: 2, rowVersion: SAMPLE_ITEMS[1].rowVersion },
      ],
    });
    expect(component.bulkPending()).toBe(true);

    const updatedSecond: RequestRecord = { ...SAMPLE_ITEMS[1], status: 'Completed', allowedNextStatuses: [], rowVersion: 'AAAAAAAAAAk=' };
    req.flush({
      requested: 2,
      updated: 1,
      failed: 1,
      results: [
        { id: 1, outcome: 'InvalidTransition', error: "Cannot change status from 'New' to 'Completed'.", request: null },
        { id: 2, outcome: 'Updated', error: null, request: updatedSecond },
      ],
    });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.bulkPending()).toBe(false);
    expect(component.pagedResult()?.items[1]).toEqual(updatedSecond);
    expect(component.pagedResult()?.items[0]).toEqual(SAMPLE_ITEMS[0]);
    expect([...component.selectedIds()]).toEqual([1]); // the failed one stays selected

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('עודכנו 1 מתוך 2');
    expect(text).toContain('נכשלו 1');
    expect(text).toContain('First request');
    expect(text).toContain('המעבר אינו מותר');

    // Something changed, so the aggregations are refreshed; no conflict, so no page reload.
    httpMock.expectOne(`${baseUrl}/summary`).flush(SAMPLE_SUMMARY);
    httpMock.expectNone((r) => r.url === baseUrl && r.method === 'GET');
  });

  it('reloads the page after a bulk conflict, since its rowVersions are stale', async () => {
    flushInitialLoad();
    selectBothRows();
    getBulkBar()!.apply.emit('InProgress');

    httpMock.expectOne(`${baseUrl}/status/bulk`).flush({
      requested: 2,
      updated: 0,
      failed: 2,
      results: [
        { id: 1, outcome: 'Conflict', error: 'modified by another user', request: null },
        { id: 2, outcome: 'InvalidTransition', error: 'already InProgress', request: null },
      ],
    });
    fixture.detectChanges();
    await fixture.whenStable();

    httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET').flush(SAMPLE_PAGED);
    httpMock.expectNone(`${baseUrl}/summary`); // nothing was updated
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('עודכנה בינתיים על ידי משתמש אחר');
  });

  it('header checkbox selects the whole page; new page data clears the selection', async () => {
    flushInitialLoad();
    getTable().allSelectionToggle.emit(true);
    fixture.detectChanges();
    expect(component.selectedIds().size).toBe(2);

    getTable().pageChange.emit({ pageIndex: 1, pageSize: 25, length: 200, previousPageIndex: 0 });
    fixture.detectChanges();
    await fixture.whenStable();
    httpMock.expectOne((r) => r.url === baseUrl && r.params.get('page') === '2').flush(SAMPLE_PAGED);
    fixture.detectChanges();

    expect(component.selectedIds().size).toBe(0);
    expect(getBulkBar()).toBeNull();
  });

  it('blocks a bulk update without a user name and sends nothing', () => {
    flushInitialLoad();
    selectBothRows();
    TestBed.inject(CurrentUserService).setUserName('');

    getBulkBar()!.apply.emit('Completed');

    httpMock.expectNone(`${baseUrl}/status/bulk`);
    expect(component.bulkPending()).toBe(false);
  });

  it('a request-level rejection (400) changes nothing and explains why', () => {
    flushInitialLoad();
    selectBothRows();
    const snackBarOpen = vi.spyOn(TestBed.inject(MatSnackBar), 'open');

    getBulkBar()!.apply.emit('Completed');
    httpMock.expectOne(`${baseUrl}/status/bulk`).flush(
      { status: 400, errors: { Items: ['Items must contain at most 100 requests.'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(component.bulkPending()).toBe(false);
    expect(component.pagedResult()?.items).toEqual(SAMPLE_ITEMS);
    expect(component.selectedIds().size).toBe(2);
    expect(snackBarOpen).toHaveBeenCalledWith(expect.stringContaining('Items must contain at most 100'), 'סגור', expect.anything());
  });

  it('explains a rejected update (400) with the server reason and reloads the stale row', async () => {
    flushInitialLoad();
    const snackBarOpen = vi.spyOn(TestBed.inject(MatSnackBar), 'open');

    const row = SAMPLE_ITEMS[0];
    getTable().statusChange.emit({ row, status: 'InProgress' });
    httpMock.expectOne(`${baseUrl}/${row.id}/status`).flush(
      { status: 400, detail: "Cannot change status from 'Completed' to 'InProgress'. 'Completed' is a final status." },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    await fixture.whenStable();

    expect(snackBarOpen).toHaveBeenCalledWith(
      "לא ניתן לעדכן את הסטטוס: Cannot change status from 'Completed' to 'InProgress'. 'Completed' is a final status.",
      'סגור',
      expect.anything(),
    );
    expect(component.updatingIds().has(row.id)).toBe(false);
    httpMock.expectOne((r) => r.url === baseUrl && r.method === 'GET').flush(SAMPLE_PAGED);
  });

  it('blocks a status update without a user name, explaining why, and sends nothing', () => {
    flushInitialLoad();
    TestBed.inject(CurrentUserService).setUserName('');
    const snackBarOpen = vi.spyOn(TestBed.inject(MatSnackBar), 'open');

    const row = SAMPLE_ITEMS[0];
    getTable().statusChange.emit({ row, status: 'InProgress' });

    httpMock.expectNone(`${baseUrl}/${row.id}/status`);
    expect(component.updatingIds().has(row.id)).toBe(false);
    expect(snackBarOpen).toHaveBeenCalledWith(expect.stringContaining('יש להזין את שמך'), 'סגור', expect.anything());
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
