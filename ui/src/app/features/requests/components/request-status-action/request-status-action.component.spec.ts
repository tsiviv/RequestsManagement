import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { RequestStatusActionComponent } from './request-status-action.component';
import { RequestStatus } from '../../models/request.model';

@Component({
  imports: [RequestStatusActionComponent],
  template: `<app-request-status-action [status]="status()" requestTitle="Sample" (statusChange)="emitted.push($event)" />`,
})
class HostComponent {
  readonly status = signal<RequestStatus>('New');
  readonly emitted: RequestStatus[] = [];
}

describe('RequestStatusActionComponent', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const action = fixture.debugElement.query(By.directive(RequestStatusActionComponent))
      .componentInstance as RequestStatusActionComponent;
    // The select's form control (protected); driving it directly is what choosing an option does.
    const control = (action as unknown as { control: { value: RequestStatus; setValue(v: RequestStatus): void } }).control;
    return { fixture, host: fixture.componentInstance, control };
  }

  it('emits the chosen status but keeps showing the saved one until the parent confirms', () => {
    const { host, control } = setup();

    control.setValue('InProgress');

    expect(host.emitted).toEqual(['InProgress']);
    // Not saved yet (and may be rejected or blocked): the select must not claim otherwise.
    expect(control.value).toBe('New');
  });

  it('shows the new status once the parent passes it in after a successful save', () => {
    const { fixture, host, control } = setup();
    control.setValue('InProgress');

    host.status.set('InProgress');
    fixture.detectChanges();

    expect(control.value).toBe('InProgress');
  });
});
