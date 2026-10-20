import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUserService } from '../user/current-user.service';
import { environment } from '../../../environments/environment';

export const USER_NAME_HEADER = 'X-User-Name';

const READ_ONLY_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Sends the acting user's name with changes (the server records it as ChangedBy).
 * Percent-encoded because header values cannot carry Hebrew: browsers refuse non-Latin-1 header
 * text, so the server decodes it (HeaderCurrentUser). Only attached to our own API, never to
 * other hosts, and only to changes; reads stay anonymous.
 */
export const userNameInterceptor: HttpInterceptorFn = (req, next) => {
  const userName = inject(CurrentUserService).userName();

  if (!userName || READ_ONLY_METHODS.has(req.method) || !req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { [USER_NAME_HEADER]: encodeURIComponent(userName) } }));
};
