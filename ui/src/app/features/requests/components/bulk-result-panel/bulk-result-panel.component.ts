import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { BulkUpdateOutcome, BulkUpdateStatusResult } from '../../models/bulk-update-status.model';

const OUTCOME_LABELS: Record<Exclude<BulkUpdateOutcome, 'Updated'>, string> = {
  Conflict: 'עודכנה בינתיים על ידי משתמש אחר',
  InvalidTransition: 'המעבר אינו מותר מהסטטוס הנוכחי שלה',
  NotFound: 'לא נמצאה',
};

/** Outcome of the last bulk update: how many succeeded, and which failed and why. */
@Component({
  selector: 'app-bulk-result-panel',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './bulk-result-panel.component.html',
  styleUrl: './bulk-result-panel.component.scss',
})
export class BulkResultPanelComponent {
  readonly result = input.required<BulkUpdateStatusResult>();
  /** Titles of the submitted requests, captured at submit time (rows may since have reloaded). */
  readonly titles = input.required<ReadonlyMap<number, string>>();
  readonly dismiss = output<void>();

  protected readonly failures = computed(() =>
    this.result()
      .results.filter((r) => r.outcome !== 'Updated')
      .map((r) => ({
        id: r.id,
        title: this.titles().get(r.id) ?? `בקשה ${r.id}`,
        reason: OUTCOME_LABELS[r.outcome as Exclude<BulkUpdateOutcome, 'Updated'>],
        serverError: r.error,
      })),
  );
}
