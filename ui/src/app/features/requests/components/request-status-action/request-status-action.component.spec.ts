import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { MatSelect } from '@angular/material/select';
import { RequestStatusActionComponent } from './request-status-action.component';
import { RequestStatus } from '../../models/request.model';

@Component({
  imports: [RequestStatusActionComponent],
  template: `
    <app-request-status-action
      [status]="status()"
      [allowedStatuses]="allowed()"
      requestTitle="Sample"
      (statusChange)="emitted.push($event)"
    />
  `,
})
class HostComponent {
  readonly status = signal<RequestStatus>('New');
  readonly allowed = signal<RequestStatus[]>(['InProgress']);
  readonly emitted: RequestStatus[] = [];
}

describe('RequestStatusActionComponent', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const action = fixture.debugElement.query(By.directive(RequestStatusActionComponent))
      .componentInstance as RequestStatusActionComponent;
    // The select's form control (protected); driving it directly is what choosing an option does.
    const control = (action as unknown as {
      control: { value: RequestStatus; disabled: boolean; setValue(v: RequestStatus): void };
    }).control;
    const select = fixture.debugElement.query(By.directive(MatSelect)).componentInstance as MatSelect;
    const optionValues = () => select.options.map((o) => o.value as RequestStatus);
    return { fixture, host: fixture.componentInstance, control, optionValues };
  }

  it('offers only the current status and the transitions the server allows', () => {
    const { fixture, host, optionValues } = setup();
    expect(optionValues()).toEqual(['New', 'InProgress']);

    host.status.set('Waiting');
    host.allowed.set(['InProgress', 'Completed']);
    fixture.detectChanges();

    expect(optionValues()).toEqual(['Waiting', 'InProgress', 'Completed']);
  });

  it('is disabled for a final status, where nothing is allowed', () => {
    const { fixture, host, control } = setup();
    expect(control.disabled).toBe(false);

    host.status.set('Completed');
    host.allowed.set([]);
    fixture.detectChanges();

    expect(control.disabled).toBe(true);
    const select = (fixture.nativeElement as HTMLElement).querySelector('mat-select')!;
    expect(select.getAttribute('aria-label')).toContain('סטטוס סופי');
  });

  it('emits the chosen status but keeps showing the saved one until the parent confirms', () => {
    const { host, control } = setup();

    control.setValue('InProgress');

    expect(host.emitted).toEqual(['InProgress']);
    // Not saved yet (and may be rejected or blocked): the select must not claim otherwise.
    expect(control.value).toBe('New');
  });

  it('shows the new status and its next options once the parent passes them in after a save', () => {
    const { fixture, host, control, optionValues } = setup();
    control.setValue('InProgress');

    host.status.set('InProgress');
    host.allowed.set(['Waiting', 'Completed']);
    fixture.detectChanges();

    expect(control.value).toBe('InProgress');
    expect(optionValues()).toEqual(['InProgress', 'Waiting', 'Completed']);
  });
});
