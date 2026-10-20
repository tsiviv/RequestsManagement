import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, tap } from 'rxjs';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Sort } from '@angular/material/sort';
import { PageEvent } from '@angular/material/paginator';
import { RequestsService } from './requests.service';
import { CurrentUserService } from '../../core/user/current-user.service';
import { MatDialog } from '@angular/material/dialog';
import {
  RequestHistoryDialogComponent,
  RequestHistoryDialogData,
} from './components/request-history-dialog/request-history-dialog.component';
import { RequestQuery } from './models/request-query.model';
import { RequestSummary } from './models/request-summary.model';
import { EMPTY_FILTER_OPTIONS } from './models/request-filter-options.model';
import { RequestRecord } from './models/request.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { ApiError } from '../../shared/models/api-error.model';
import {
  RequestFilters,
  RequestFiltersComponent,
} from './components/request-filters/request-filters.component';
import { RequestSummaryComponent } from './components/request-summary/request-summary.component';
import {
  RequestStatusChangeEvent,
  RequestsTableComponent,
} from './components/requests-table/requests-table.component';
import { REQUEST_STATUSES, REQUEST_STATUS_LABELS, RequestStatus } from './models/request.model';
import { BulkUpdateStatusResult } from './models/bulk-update-status.model';
import { BulkActionBarComponent } from './components/bulk-action-bar/bulk-action-bar.component';
import { BulkResultPanelComponent } from './components/bulk-result-panel/bulk-result-panel.component';

const DEFAULT_QUERY: RequestQuery = {
  page: 1,
  pageSize: 25,
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

@Component({
  selector: 'app-requests-page',
  imports: [
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    RequestSummaryComponent,
    RequestFiltersComponent,
    RequestsTableComponent,
    BulkActionBarComponent,
    BulkResultPanelComponent,
  ],
  templateUrl: './requests-page.component.html',
  styleUrl: './requests-page.component.scss',
})
export class RequestsPageComponent {
  private readonly requestsService = inject(RequestsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly currentUser = inject(CurrentUserService);
  private readonly dialog = inject(MatDialog);

  private readonly _query = signal<RequestQuery>(DEFAULT_QUERY);
  private readonly _pagedResult = signal<PagedResult<RequestRecord> | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private readonly _summary = signal<RequestSummary | null>(null);
  private readonly _summaryLoading = signal(false);
  private readonly _summaryError = signal<ApiError | null>(null);

  private readonly _updatingIds = signal<ReadonlySet<number>>(new Set());

  // Bulk update. Selection is per page: it is cleared whenever the page shows new data, so it
  // never holds rowVersions of rows that are no longer on screen (a page holds at most 100 rows,
  // the server's bulk limit).
  private readonly _selectedIds = signal<ReadonlySet<number>>(new Set());
  private readonly _bulkPending = signal(false);
  private readonly _bulkResult = signal<{ result: BulkUpdateStatusResult; titles: ReadonlyMap<number, string> } | null>(
    null,
  );

  /** Read-only view of state for the template and tests; all writes go through the private signals above. */
  protected readonly query = this._query.asReadonly();
  readonly pagedResult = this._pagedResult.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly summary = this._summary.asReadonly();
  readonly summaryLoading = this._summaryLoading.asReadonly();
  readonly summaryError = this._summaryError.asReadonly();
  readonly updatingIds = this._updatingIds.asReadonly();
  readonly selectedIds = this._selectedIds.asReadonly();
  readonly bulkPending = this._bulkPending.asReadonly();
  readonly bulkResult = this._bulkResult.asReadonly();

  private readonly selectedRows = computed(() =>
    (this._pagedResult()?.items ?? []).filter((row) => this._selectedIds().has(row.id)),
  );

  /** Statuses at least one selected request may move to, in the usual status order. */
  protected readonly bulkTargetStatuses = computed(() => {
    const allowed = new Set(this.selectedRows().flatMap((row) => row.allowedNextStatuses));
    return REQUEST_STATUSES.filter((status) => allowed.has(status));
  });

  private readonly reloadToken = signal(0);
  private readonly summaryReloadToken = signal(0);

  protected readonly isFiltered = computed(() => {
    const q = this._query();
    return Boolean(
      q.search || q.status || q.priority || q.organizationName || q.assignedTo || q.createdFrom || q.createdTo,
    );
  });

  /**
   * Choices for the organization / handler filters, loaded once. If this fails the page still works:
   * those two lists are just empty (only "all"), every other filter is unaffected.
   */
  protected readonly filterOptions = toSignal(
    this.requestsService.getFilterOptions().pipe(catchError(() => of(EMPTY_FILTER_OPTIONS))),
    { initialValue: EMPTY_FILTER_OPTIONS },
  );

  private readonly requestsTrigger = computed(() => ({
    query: this._query(),
    tick: this.reloadToken(),
  }));

  private readonly requests$ = toObservable(this.requestsTrigger).pipe(
    tap(() => this._loading.set(true)),
    switchMap(({ query }) =>
      this.requestsService.getRequests(query).pipe(
        catchError((error: ApiError) => {
          this._error.set(error);
          return of(null);
        }),
      ),
    ),
    takeUntilDestroyed(),
  );

  private readonly summary$ = toObservable(this.summaryReloadToken).pipe(
    tap(() => this._summaryLoading.set(true)),
    switchMap(() =>
      this.requestsService.getSummary().pipe(
        catchError((error: ApiError) => {
          this._summaryError.set(error);
          return of(null);
        }),
      ),
    ),
    takeUntilDestroyed(),
  );

  constructor() {
    this.requests$.subscribe((result) => {
      this._loading.set(false);
      if (result) {
        this._error.set(null);
        this._pagedResult.set(result);
        this._selectedIds.set(new Set());
      }
    });

    this.summary$.subscribe((result) => {
      this._summaryLoading.set(false);
      if (result) {
        this._summaryError.set(null);
        this._summary.set(result);
      }
    });
  }

  protected onFiltersChange(filters: RequestFilters): void {
    this._query.update((q) => ({
      ...q,
      search: filters.search || undefined,
      status: filters.status ?? undefined,
      priority: filters.priority ?? undefined,
      organizationName: filters.organizationName ?? undefined,
      assignedTo: filters.assignedTo ?? undefined,
      createdFrom: filters.createdFrom ?? undefined,
      createdTo: filters.createdTo ?? undefined,
      page: 1,
    }));
  }

  protected onSortChange(sort: Sort): void {
    this._query.update((q) => ({
      ...q,
      sortBy: sort.active as RequestQuery['sortBy'],
      sortDirection: sort.direction as RequestQuery['sortDirection'],
      page: 1,
    }));
  }

  protected onPageChange(event: PageEvent): void {
    this._query.update((q) => ({
      ...q,
      page: event.pageIndex + 1,
      pageSize: event.pageSize,
    }));
  }

  protected reloadRequests(): void {
    this.reloadToken.update((tick) => tick + 1);
  }

  protected reloadSummary(): void {
    this.summaryReloadToken.update((tick) => tick + 1);
  }

  protected openHistory(row: RequestRecord): void {
    this.dialog.open<RequestHistoryDialogComponent, RequestHistoryDialogData>(RequestHistoryDialogComponent, {
      data: { requestId: row.id, requestTitle: row.title },
      width: '32rem',
      maxWidth: '95vw',
      autoFocus: 'dialog',
    });
  }

  protected onStatusChange(event: RequestStatusChangeEvent): void {
    const { row, status } = event;
    if (this._updatingIds().has(row.id)) {
      return;
    }

    if (!this.ensureUserName()) {
      return;
    }

    this.setUpdating(row.id, true);

    this.requestsService.updateStatus(row.id, { status, rowVersion: row.rowVersion }).subscribe({
      next: (updated) => {
        this.setUpdating(row.id, false);
        this.replaceRows([updated]);
        this.snackBar.open(`הסטטוס עודכן ל${REQUEST_STATUS_LABELS[updated.status]}.`, 'סגור', {
          duration: 4000,
        });
        this.reloadSummary();
      },
      error: (apiError: ApiError) => {
        this.setUpdating(row.id, false);
        this.snackBar.open(updateErrorMessage(apiError), 'סגור', { duration: 8000 });
        // Conflict / not found / rejected transition all mean the row on screen no longer matches
        // the server (status, rowVersion or allowed next statuses), so fetch the current page again.
        if (apiError.kind === 'conflict' || apiError.kind === 'not-found' || apiError.kind === 'validation') {
          this.reloadRequests();
        }
      },
    });
  }

  protected toggleRowSelection(row: RequestRecord): void {
    this._selectedIds.update((current) => {
      const next = new Set(current);
      if (!next.delete(row.id)) {
        next.add(row.id);
      }
      return next;
    });
  }

  protected toggleAllSelection(selectAll: boolean): void {
    this._selectedIds.set(new Set(selectAll ? (this._pagedResult()?.items ?? []).map((row) => row.id) : []));
  }

  protected clearSelection(): void {
    this._selectedIds.set(new Set());
  }

  protected dismissBulkResult(): void {
    this._bulkResult.set(null);
  }

  protected onBulkApply(status: RequestStatus): void {
    const rows = this.selectedRows();
    if (this._bulkPending() || rows.length === 0 || !this.ensureUserName()) {
      return;
    }

    // Titles are kept with the result: the rows may be reloaded before the user reads it.
    const titles = new Map(rows.map((row) => [row.id, row.title]));
    this._bulkPending.set(true);
    this._bulkResult.set(null);

    this.requestsService
      .bulkUpdateStatus({ status, items: rows.map((row) => ({ id: row.id, rowVersion: row.rowVersion })) })
      .subscribe({
        next: (result) => {
          this._bulkPending.set(false);
          this._bulkResult.set({ result, titles });
          this.replaceRows(result.results.flatMap((r) => (r.request ? [r.request] : [])));
          // Failed rows stay selected so the user can see them and retry (e.g. a different status).
          this._selectedIds.set(new Set(result.results.filter((r) => r.outcome !== 'Updated').map((r) => r.id)));

          if (result.updated > 0) {
            this.reloadSummary();
          }
          // Conflicts and missing requests mean the page is stale; fetch it again (fresh rowVersions).
          if (result.results.some((r) => r.outcome === 'Conflict' || r.outcome === 'NotFound')) {
            this.reloadRequests();
          }
        },
        error: (apiError: ApiError) => {
          // Request-level rejection (400): nothing was updated.
          this._bulkPending.set(false);
          this.snackBar.open(updateErrorMessage(apiError), 'סגור', { duration: 8000 });
        },
      });
  }

  /** The server rejects changes without a name (400); say why up front instead of a generic error. */
  private ensureUserName(): boolean {
    if (this.currentUser.hasUserName()) {
      return true;
    }
    this.snackBar.open('יש להזין את שמך בשדה שבראש העמוד לפני עדכון סטטוס. השם נרשם בהיסטוריית השינויים.', 'סגור', {
      duration: 6000,
    });
    return false;
  }

  /** Puts the server's updated versions (new status, rowVersion, allowed next statuses) in place. */
  private replaceRows(updated: readonly RequestRecord[]): void {
    if (updated.length === 0) {
      return;
    }
    const byId = new Map(updated.map((row) => [row.id, row]));
    this._pagedResult.update((current) =>
      current ? { ...current, items: current.items.map((item) => byId.get(item.id) ?? item) } : current,
    );
  }

  private setUpdating(id: number, pending: boolean): void {
    this._updatingIds.update((current) => {
      const next = new Set(current);
      if (pending) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }
}

/** A rejected update (400) explains why, using the server's reason; other kinds keep their message. */
function updateErrorMessage(error: ApiError): string {
  if (error.kind === 'validation') {
    return error.detail ? `לא ניתן לעדכן את הסטטוס: ${error.detail}` : 'לא ניתן לעדכן את הסטטוס.';
  }
  return error.message;
}
