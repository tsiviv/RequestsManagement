import { Component, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, merge } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
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
  ],
  templateUrl: './request-filters.component.html',
  styleUrl: './request-filters.component.scss',
})
export class RequestFiltersComponent {
  readonly filtersChange = output<RequestFilters>();

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly priorities = REQUEST_PRIORITIES;
  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly priorityLabels = REQUEST_PRIORITY_LABELS;

  readonly form = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    status: new FormControl<RequestStatus | ''>('', { nonNullable: true }),
    priority: new FormControl<RequestPriority | ''>('', { nonNullable: true }),
  });

  constructor() {
    const search$ = this.form.controls.search.valueChanges.pipe(
      debounceTime(SEARCH_DEBOUNCE_MS),
      distinctUntilChanged(),
    );
    const status$ = this.form.controls.status.valueChanges;
    const priority$ = this.form.controls.priority.valueChanges;

    merge(search$, status$, priority$)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.emitFilters());
  }

  resetFilters(): void {
    this.form.reset({ search: '', status: '', priority: '' }, { emitEvent: false });
    this.emitFilters();
  }

  private emitFilters(): void {
    const value = this.form.getRawValue();
    this.filtersChange.emit({
      search: value.search.trim(),
      status: value.status || null,
      priority: value.priority || null,
    });
  }
}
