import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { RequestHistoryDialogComponent } from './request-history-dialog.component';
import { RequestStatusHistoryEntry } from '../../models/request-history.model';
import { httpErrorInterceptor } from '../../../../core/interceptors/http-error.interceptor';
import { environment } from '../../../../../environments/environment';

const historyUrl = `${environment.apiUrl}/requests/7/history`;

const ENTRIES: RequestStatusHistoryEntry[] = [
  { id: 2, previousStatus: 'InProgress', newStatus: 'Waiting', changedAt: '2026-08-02T09:30:00Z', changedBy: 'יוסי לוי' },
  { id: 1, previousStatus: 'New', newStatus: 'InProgress', changedAt: '2026-08-01T08:00:00Z', changedBy: 'דנה כהן' },
];

describe('RequestHistoryDialogComponent', () => {
  let httpMock: HttpTestingController;

  function open() {
    TestBed.configureTestingModule({
      imports: [RequestHistoryDialogComponent],
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { requestId: 7, requestTitle: 'תקלת שירות' } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RequestHistoryDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  afterEach(() => httpMock.verify());

  it('shows a loading state, then the changes newest first as returned by the API', () => {
    const fixture = open();
    expect(text(fixture)).toContain('טוען היסטוריה');
    expect(text(fixture)).toContain('תקלת שירות');

    httpMock.expectOne(historyUrl).flush(ENTRIES);
    fixture.detectChanges();

    const items = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('בטיפול');
    expect(items[0].textContent).toContain('ממתין');
    expect(items[0].textContent).toContain('יוסי לוי');
    expect(items[1].textContent).toContain('חדש');
    expect(items[1].textContent).toContain('דנה כהן');
  });

  it('shows an empty state when the status was never changed', () => {
    const fixture = open();
    httpMock.expectOne(historyUrl).flush([]);
    fixture.detectChanges();

    expect(text(fixture)).toContain('עדיין לא שונה');
    expect((fixture.nativeElement as HTMLElement).querySelector('li')).toBeNull();
  });

  it('shows an error with a retry that loads the history again', () => {
    const fixture = open();
    httpMock.expectOne(historyUrl).flush('down', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text(fixture)).toContain('לא ניתן לטעון את ההיסטוריה');

    const retry = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find((b) =>
      b.textContent?.includes('נסה שוב'),
    )!;
    retry.click();
    fixture.detectChanges();
    expect(text(fixture)).toContain('טוען היסטוריה');

    httpMock.expectOne(historyUrl).flush(ENTRIES);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('li')).toHaveLength(2);
  });
});
