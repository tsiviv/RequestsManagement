import { Component, computed, effect, input, output, untracked } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { REQUEST_STATUS_LABELS, RequestStatus } from '../../models/request.model';

@Component({
  selector: 'app-request-status-action',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatSelectModule, MatProgressSpinnerModule],
  templateUrl: './request-status-action.component.html',
  styleUrl: './request-status-action.component.scss',
})
export class RequestStatusActionComponent {
  readonly status = input.required<RequestStatus>();
  /** Valid next statuses as decided by the server (RequestDto.allowedNextStatuses); empty for a final status. */
  readonly allowedStatuses = input.required<readonly RequestStatus[]>();
  readonly requestTitle = input.required<string>();
  readonly pending = input(false);
  readonly statusChange = output<RequestStatus>();

  protected readonly statusLabels = REQUEST_STATUS_LABELS;
  protected readonly control = new FormControl<RequestStatus>('New', { nonNullable: true });

  /** The current status (shown as selected) followed by the statuses it may move to. */
  protected readonly options = computed(() => [this.status(), ...this.allowedStatuses()]);
  protected readonly isFinal = computed(() => this.allowedStatuses().length === 0);

  constructor() {
    effect(() => {
      const current = this.status();
      untracked(() => this.control.setValue(current, { emitEvent: false }));
    });

    effect(() => {
      const disabled = this.pending() || this.isFinal();
      untracked(() => {
        if (disabled) {
          this.control.disable({ emitEvent: false });
        } else {
          this.control.enable({ emitEvent: false });
        }
      });
    });

    this.control.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      if (value !== this.status()) {
        this.statusChange.emit(value);
        // The select shows the request's actual status, not the requested one: it only changes
        // when the parent passes in the updated status after a successful save. Otherwise a
        // rejected or blocked update would leave the select showing a status that was never saved.
        this.control.setValue(this.status(), { emitEvent: false });
      }
    });
  }
}
