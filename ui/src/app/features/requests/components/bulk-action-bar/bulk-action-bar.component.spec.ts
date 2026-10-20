import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { BulkActionBarComponent } from './bulk-action-bar.component';
import { RequestStatus } from '../../models/request.model';

@Component({
  imports: [BulkActionBarComponent],
  template: `
    <app-bulk-action-bar
      [selectedCount]="count()"
      [targetStatuses]="targets()"
      [pending]="pending()"
      (apply)="applied.push($event)"
    />
  `,
})
class HostComponent {
  readonly count = signal(2);
  readonly targets = signal<RequestStatus[]>(['InProgress', 'Completed']);
  readonly pending = signal(false);
  readonly applied: RequestStatus[] = [];
}

describe('BulkActionBarComponent', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const bar = fixture.debugElement.query(By.directive(BulkActionBarComponent)).componentInstance as BulkActionBarComponent;
    const applyButton = () =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((b) =>
        b.textContent?.includes('עדכון הנבחרות'),
      );
    return { fixture, host: fixture.componentInstance, bar, applyButton };
  }

  it('applies only once a target status is chosen, and not while a bulk update is running', () => {
    const { fixture, host, bar, applyButton } = setup();
    expect(applyButton()!.disabled).toBe(true);

    bar.target.setValue('Completed');
    fixture.detectChanges();
    applyButton()!.click();
    expect(host.applied).toEqual(['Completed']);

    host.pending.set(true);
    fixture.detectChanges();
    expect(applyButton()!.disabled).toBe(true);
  });

  it('drops a chosen target that no selected request can move to anymore', () => {
    const { fixture, host, bar } = setup();
    bar.target.setValue('Completed');

    host.targets.set(['InProgress']);
    fixture.detectChanges();

    expect(bar.target.value).toBe('');
  });

  it('explains when the selected requests are all in a final status', () => {
    const { fixture, host, applyButton } = setup();
    host.targets.set([]);
    fixture.detectChanges();

    expect(applyButton()).toBeUndefined();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('סטטוס סופי');
  });
});
