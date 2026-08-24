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
      message: 'Unable to connect to the server. Please check your connection.',
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
      return 'Please check the entered values.';
    case 'not-found':
      return 'The request could not be found.';
    case 'conflict':
      return 'The request was updated by another user. Please refresh the request before making another change.';
    case 'server':
      return 'Something went wrong on the server. Please try again.';
    default:
      return 'An unexpected error occurred.';
  }
}
