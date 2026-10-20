import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RequestsService } from '../../requests.service';
import { RequestStatusHistoryEntry } from '../../models/request-history.model';
import { REQUEST_STATUS_LABELS } from '../../models/request.model';
import { ApiError } from '../../../../shared/models/api-error.model';

export interface RequestHistoryDialogData {
  requestId: number;
  requestTitle: string;
}

type HistoryState =
  | { kind: 'loading' }
  | { kind: 'error'; error: ApiError }
  | { kind: 'loaded'; entries: RequestStatusHistoryEntry[] };

/** Status change history of one request, newest first (the order the API returns). */
@Component({
  selector: 'app-request-history-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './request-history-dialog.component.html',
  styleUrl: './request-history-dialog.component.scss',
})
export class RequestHistoryDialogComponent {
  protected readonly data = inject<RequestHistoryDialogData>(MAT_DIALOG_DATA);
  private readonly requestsService = inject(RequestsService);

  protected readonly statusLabels = REQUEST_STATUS_LABELS;

  private readonly reloadToken = signal(0);
  readonly state = signal<HistoryState>({ kind: 'loading' });
  protected readonly entries = computed(() => {
    const state = this.state();
    return state.kind === 'loaded' ? state.entries : [];
  });

  constructor() {
    toObservable(this.reloadToken)
      .pipe(
        switchMap(() =>
          this.requestsService.getHistory(this.data.requestId).pipe(
            map((entries): HistoryState => ({ kind: 'loaded', entries })),
            catchError((error: ApiError) => of<HistoryState>({ kind: 'error', error })),
            startWith<HistoryState>({ kind: 'loading' }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected retry(): void {
    this.reloadToken.update((tick) => tick + 1);
  }
}
