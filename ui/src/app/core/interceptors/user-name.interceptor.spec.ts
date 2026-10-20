import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { USER_NAME_HEADER, userNameInterceptor } from './user-name.interceptor';
import { CurrentUserService } from '../user/current-user.service';
import { environment } from '../../../environments/environment';

describe('userNameInterceptor', () => {
  const apiUrl = `${environment.apiUrl}/requests/1/status`;
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let currentUser: CurrentUserService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([userNameInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    currentUser = TestBed.inject(CurrentUserService);
    currentUser.setUserName('דנה כהן');
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('adds the percent-encoded name to changes sent to the API', () => {
    http.patch(apiUrl, {}).subscribe();
    http.post(`${environment.apiUrl}/requests/status/bulk`, {}).subscribe();

    for (const req of httpMock.match(() => true)) {
      // Hebrew cannot travel in a header as-is; the server decodes this back (HeaderCurrentUser).
      expect(req.request.headers.get(USER_NAME_HEADER)).toBe('%D7%93%D7%A0%D7%94%20%D7%9B%D7%94%D7%9F');
      req.flush({});
    }
  });

  it('does not send the name with reads', () => {
    http.get(apiUrl).subscribe();
    expect(httpMock.expectOne(apiUrl).request.headers.has(USER_NAME_HEADER)).toBe(false);
  });

  it('never sends the name to other hosts', () => {
    http.post('https://example.com/collect', {}).subscribe();
    expect(httpMock.expectOne('https://example.com/collect').request.headers.has(USER_NAME_HEADER)).toBe(false);
  });

  it('sends no header when no name was entered (the server then answers 400)', () => {
    currentUser.setUserName('');
    http.patch(apiUrl, {}).subscribe();
    expect(httpMock.expectOne(apiUrl).request.headers.has(USER_NAME_HEADER)).toBe(false);
  });
});
