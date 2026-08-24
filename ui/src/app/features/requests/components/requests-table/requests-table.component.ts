import { Component, computed, inject, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { BreakpointObserver } from '@angular/cdk/layout';
import {
  REQUEST_PRIORITY_ICONS,
  REQUEST_PRIORITY_LABELS,
  REQUEST_STATUS_ICONS,
  REQUEST_STATUS_LABELS,
  RequestRecord,
  RequestStatus,
} from '../../models/request.model';
import { RequestSortField } from '../../models/request-query.model';
import { RequestStatusActionComponent } from '../request-status-action/request-status-action.component';

export interface RequestStatusChangeEvent {
  row: RequestRecord;
  status: RequestStatus;
}

const ALL_COLUMNS = [
  'id',
  'title',
  'organizationName',
  'status',
  'priority',
  'createdAt',
  'updatedAt',
  'actions',
] as const;

const HANDSET_COLUMNS = ['title', 'organizationName', 'status', 'priority', 'actions'] as const;

const HANDSET_BREAKPOINT = '(max-width: 799.98px)';

@Component({
  selector: 'app-requests-table',
  imports: [
    DatePipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatIconModule,
    RequestStatusActionComponent,
  ],
  templateUrl: './requests-table.component.html',
  styleUrl: './requests-table.component.scss',
})
export class RequestsTableComponent {
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly items = input.required<RequestRecord[]>();
  readonly totalCount = input.required<number>();
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly sortBy = input.required<RequestSortField>();
  readonly sortDirection = input.required<'asc' | 'desc'>();
  readonly updatingIds = input<ReadonlySet<number>>(new Set());

  readonly sortChange = output<Sort>();
  readonly pageChange = output<PageEvent>();
  readonly statusChange = output<RequestStatusChangeEvent>();

  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly statusIcons = REQUEST_STATUS_ICONS;
  protected readonly priorityLabels = REQUEST_PRIORITY_LABELS;
  protected readonly priorityIcons = REQUEST_PRIORITY_ICONS;
  protected readonly pageSizeOptions = [10, 25, 50, 100];

  private readonly isHandset = toSignal(
    this.breakpointObserver.observe(HANDSET_BREAKPOINT).pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly displayedColumns = computed<string[]>(() =>
    this.isHandset() ? [...HANDSET_COLUMNS] : [...ALL_COLUMNS],
  );

  protected onStatusChange(row: RequestRecord, status: RequestStatus): void {
    this.statusChange.emit({ row, status });
  }

  // matCellDef row templates are untyped in Angular Material's current typings, so cell
  // markup routes lookups through these typed helpers instead of indexing Records inline.
  protected statusLabel(row: RequestRecord): string {
    return this.statusLabels[row.status];
  }

  protected statusIcon(row: RequestRecord): string {
    return this.statusIcons[row.status];
  }

  protected statusBadgeClass(row: RequestRecord): string {
    return 'badge--status-' + row.status.toLowerCase();
  }

  protected priorityLabel(row: RequestRecord): string {
    return this.priorityLabels[row.priority];
  }

  protected priorityIcon(row: RequestRecord): string {
    return this.priorityIcons[row.priority];
  }

  protected priorityBadgeClass(row: RequestRecord): string {
    return 'badge--priority-' + row.priority.toLowerCase();
  }
}
