import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUserService } from '../user/current-user.service';
import { environment } from '../../../environments/environment';

export const USER_NAME_HEADER = 'X-User-Name';

const READ_ONLY_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

// Percent-encoded: header values cannot carry Hebrew. Sent only with changes, only to our own API.
export const userNameInterceptor: HttpInterceptorFn = (req, next) => {
  const userName = inject(CurrentUserService).userName();

  if (!userName || READ_ONLY_METHODS.has(req.method) || !req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { [USER_NAME_HEADER]: encodeURIComponent(userName) } }));
};
