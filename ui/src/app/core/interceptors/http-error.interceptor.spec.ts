import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { httpErrorInterceptor } from './http-error.interceptor';
import { ApiError } from '../../shared/models/api-error.model';

describe('httpErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('maps a 409 response to a conflict ApiError', () => {
    let captured: ApiError | undefined;

    http.get('/api/requests/1').subscribe({
      error: (err: ApiError) => (captured = err),
    });

    httpMock.expectOne('/api/requests/1').flush('conflict', { status: 409, statusText: 'Conflict' });

    expect(captured?.kind).toBe('conflict');
    expect(captured?.status).toBe(409);
  });

  it('maps a 404 response to a not-found ApiError', () => {
    let captured: ApiError | undefined;

    http.get('/api/requests/1').subscribe({
      error: (err: ApiError) => (captured = err),
    });

    httpMock.expectOne('/api/requests/1').flush('missing', { status: 404, statusText: 'Not Found' });

    expect(captured?.kind).toBe('not-found');
  });

  it('maps a connection failure (status 0) to a network ApiError', () => {
    let captured: ApiError | undefined;

    http.get('/api/requests/1').subscribe({
      error: (err: ApiError) => (captured = err),
    });

    httpMock.expectOne('/api/requests/1').error(new ProgressEvent('error'), { status: 0 });

    expect(captured?.kind).toBe('network');
    expect(captured?.status).toBeNull();
  });

  it('keeps the server explanation from a ProblemDetails detail', () => {
    let captured: ApiError | undefined;
    http.patch('/api/requests/1/status', {}).subscribe({ error: (err: ApiError) => (captured = err) });

    httpMock.expectOne('/api/requests/1/status').flush(
      { status: 400, title: 'Validation failed', detail: "Cannot change status from 'New' to 'Completed'. Allowed: InProgress." },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(captured?.kind).toBe('validation');
    expect(captured?.message).toBe('אנא בדוק את הערכים שהוזנו.');
    expect(captured?.detail).toBe("Cannot change status from 'New' to 'Completed'. Allowed: InProgress.");
  });

  it('uses the first field error of a model-validation ProblemDetails', () => {
    let captured: ApiError | undefined;
    http.post('/api/requests/status/bulk', {}).subscribe({ error: (err: ApiError) => (captured = err) });

    httpMock.expectOne('/api/requests/status/bulk').flush(
      { status: 400, errors: { Items: ['Items must contain at most 100 requests.'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(captured?.detail).toBe('Items must contain at most 100 requests.');
  });

  it('has no detail when the error body is not ProblemDetails', () => {
    let captured: ApiError | undefined;
    http.get('/api/requests/1').subscribe({ error: (err: ApiError) => (captured = err) });

    httpMock.expectOne('/api/requests/1').flush('<html>Bad gateway</html>', { status: 502, statusText: 'Bad Gateway' });

    expect(captured?.kind).toBe('server');
    expect(captured?.detail).toBeUndefined();
  });

  it('maps a 500 response to a server ApiError', () => {
    let captured: ApiError | undefined;

    http.get('/api/requests/1').subscribe({
      error: (err: ApiError) => (captured = err),
    });

    httpMock
      .expectOne('/api/requests/1')
      .flush('boom', { status: 500, statusText: 'Internal Server Error' });

    expect(captured?.kind).toBe('server');
  });
});
