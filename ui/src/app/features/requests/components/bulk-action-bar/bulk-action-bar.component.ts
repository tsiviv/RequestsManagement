import { Component, effect, input, output, untracked } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { REQUEST_STATUS_LABELS, RequestStatus } from '../../models/request.model';
import { BULK_UPDATE_MAX_ITEMS } from '../../models/bulk-update-status.model';

@Component({
  selector: 'app-bulk-action-bar',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatProgressSpinnerModule, MatSelectModule],
  templateUrl: './bulk-action-bar.component.html',
  styleUrl: './bulk-action-bar.component.scss',
})
export class BulkActionBarComponent {
  readonly selectedCount = input.required<number>();
  readonly targetStatuses = input.required<readonly RequestStatus[]>();
  readonly pending = input(false);

  readonly apply = output<RequestStatus>();
  readonly clearSelection = output<void>();

  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly maxItems = BULK_UPDATE_MAX_ITEMS;
  readonly target = new FormControl<RequestStatus | ''>('', { nonNullable: true });

  constructor() {
    // A target that no selected request can move to anymore (selection changed) is dropped.
    effect(() => {
      const targets = this.targetStatuses();
      untracked(() => {
        const current = this.target.value;
        if (current && !targets.includes(current)) {
          this.target.setValue('');
        }
      });
    });
  }

  protected submit(): void {
    const status = this.target.value;
    if (status) {
      this.apply.emit(status);
    }
  }
}
