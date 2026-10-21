import { Component, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, map, merge } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import {
  REQUEST_PRIORITIES,
  REQUEST_PRIORITY_LABELS,
  REQUEST_STATUSES,
  REQUEST_STATUS_LABELS,
  RequestPriority,
  RequestStatus,
} from '../../models/request.model';

export interface RequestFilters {
  search: string;
  status: RequestStatus | null;
  priority: RequestPriority | null;
  organizationName: string | null;
  assignedTo: string | null;
  createdFrom: string | null;
  /** The day after the last included day: the API range is half-open. */
  createdTo: string | null;
}

const SEARCH_DEBOUNCE_MS = 300;

@Component({
  selector: 'app-request-filters',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './request-filters.component.html',
  styleUrl: './request-filters.component.scss',
})
export class RequestFiltersComponent {
  readonly organizations = input<readonly string[]>([]);
  readonly assignees = input<readonly string[]>([]);

  readonly filtersChange = output<RequestFilters>();

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly priorities = REQUEST_PRIORITIES;
  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly priorityLabels = REQUEST_PRIORITY_LABELS;

  readonly form = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    status: new FormControl<RequestStatus | ''>('', { nonNullable: true }),
    priority: new FormControl<RequestPriority | ''>('', { nonNullable: true }),
    organizationName: new FormControl('', { nonNullable: true }),
    assignedTo: new FormControl('', { nonNullable: true }),
    created: new FormGroup({
      start: new FormControl<Date | null>(null),
      end: new FormControl<Date | null>(null),
    }),
  });

  constructor() {
    const controls = this.form.controls;

    // Only free text is debounced; selections apply at once.
    const search$ = controls.search.valueChanges.pipe(debounceTime(SEARCH_DEBOUNCE_MS), distinctUntilChanged());
    const created$ = controls.created.valueChanges.pipe(
      map(() => this.dateRange()),
      distinctUntilChanged((a, b) => a.createdFrom === b.createdFrom && a.createdTo === b.createdTo),
    );

    merge(
      search$,
      controls.status.valueChanges,
      controls.priority.valueChanges,
      controls.organizationName.valueChanges,
      controls.assignedTo.valueChanges,
      created$,
    )
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.emitFilters());
  }

  resetFilters(): void {
    this.form.reset(
      { search: '', status: '', priority: '', organizationName: '', assignedTo: '', created: { start: null, end: null } },
      { emitEvent: false },
    );
    this.emitFilters();
  }

  private emitFilters(): void {
    const value = this.form.getRawValue();
    this.filtersChange.emit({
      search: value.search.trim(),
      status: value.status || null,
      priority: value.priority || null,
      organizationName: value.organizationName || null,
      assignedTo: value.assignedTo || null,
      ...this.dateRange(),
    });
  }

  private dateRange(): Pick<RequestFilters, 'createdFrom' | 'createdTo'> {
    const { start, end } = this.form.controls.created.getRawValue();
    return {
      createdFrom: start ? toIsoDate(start) : null,
      createdTo: end ? toIsoDate(addDays(end, 1)) : null,
    };
  }
}

/** `YYYY-MM-DD` from the local calendar date (toISOString would shift it to UTC, e.g. to the previous day in Israel). */
function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date);
  result.setDate(result.getDate() + days);
  return result;
}
