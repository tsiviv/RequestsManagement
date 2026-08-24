import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError, ApiErrorKind } from '../../shared/models/api-error.model';

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        return throwError(() => toApiError(error));
      }
      return throwError(() => error);
    }),
  );

function toApiError(error: HttpErrorResponse): ApiError {
  if (error.status === 0) {
    return {
      kind: 'network',
      message: 'Unable to reach the server. Check your connection and try again.',
      status: null,
    };
  }

  const kind = classify(error.status);
  return { kind, message: defaultMessage(kind), status: error.status };
}

function classify(status: number): ApiErrorKind {
  switch (status) {
    case 400:
      return 'validation';
    case 404:
      return 'not-found';
    case 409:
      return 'conflict';
    default:
      return status >= 500 ? 'server' : 'unknown';
  }
}

function defaultMessage(kind: ApiErrorKind): string {
  switch (kind) {
    case 'validation':
      return 'The request could not be processed. Please check your input.';
    case 'not-found':
      return 'The requested resource was not found.';
    case 'conflict':
      return 'This request was modified by another user. Please refresh the request and try again.';
    case 'server':
      return 'The server encountered an error. Please try again later.';
    default:
      return 'An unexpected error occurred.';
  }
}
