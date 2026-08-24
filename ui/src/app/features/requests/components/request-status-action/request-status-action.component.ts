import { Component, effect, input, output, untracked } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { REQUEST_STATUSES, REQUEST_STATUS_LABELS, RequestStatus } from '../../models/request.model';

@Component({
  selector: 'app-request-status-action',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatSelectModule, MatProgressSpinnerModule],
  templateUrl: './request-status-action.component.html',
  styleUrl: './request-status-action.component.scss',
})
export class RequestStatusActionComponent {
  readonly status = input.required<RequestStatus>();
  readonly requestTitle = input.required<string>();
  readonly pending = input(false);
  readonly statusChange = output<RequestStatus>();

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly control = new FormControl<RequestStatus>('New', { nonNullable: true });

  constructor() {
    effect(() => {
      const current = this.status();
      untracked(() => this.control.setValue(current, { emitEvent: false }));
    });

    effect(() => {
      const isPending = this.pending();
      untracked(() => {
        if (isPending) {
          this.control.disable({ emitEvent: false });
        } else {
          this.control.enable({ emitEvent: false });
        }
      });
    });

    this.control.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      if (value !== this.status()) {
        this.statusChange.emit(value);
      }
    });
  }
}
