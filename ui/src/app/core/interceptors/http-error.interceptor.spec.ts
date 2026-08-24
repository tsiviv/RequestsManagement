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
