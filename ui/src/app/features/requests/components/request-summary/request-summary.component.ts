import { Component, computed, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiError } from '../../../../shared/models/api-error.model';
import { REQUEST_PRIORITIES, REQUEST_PRIORITY_LABELS } from '../../models/request.model';
import { RequestSummary } from '../../models/request-summary.model';

interface StatusTile {
  label: string;
  value: number;
}

@Component({
  selector: 'app-request-summary',
  imports: [MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './request-summary.component.html',
  styleUrl: './request-summary.component.scss',
})
export class RequestSummaryComponent {
  readonly summary = input<RequestSummary | null>(null);
  readonly loading = input(false);
  readonly error = input<ApiError | null>(null);
  readonly retry = output<void>();

  protected readonly priorities = REQUEST_PRIORITIES;
  protected readonly priorityLabels = REQUEST_PRIORITY_LABELS;

  protected readonly statusTiles = computed<StatusTile[]>(() => {
    const summary = this.summary();
    if (!summary) {
      return [];
    }
    return [
      { label: 'Total Requests', value: summary.total },
      { label: 'New', value: summary.byStatus.New },
      { label: 'In Progress', value: summary.byStatus.InProgress },
      { label: 'Waiting', value: summary.byStatus.Waiting },
      { label: 'Completed', value: summary.byStatus.Completed },
    ];
  });
}
