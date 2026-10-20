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
      message: 'לא ניתן להתחבר לשרת. אנא בדוק את החיבור שלך.',
      status: null,
    };
  }

  const kind = classify(error.status);
  const detail = problemDetail(error.error);
  return { kind, message: defaultMessage(kind), status: error.status, ...(detail ? { detail } : {}) };
}

/**
 * The server answers errors with ProblemDetails: app errors carry `detail`, model validation
 * carries `errors: { field: [messages] }`. Anything else (plain text, HTML from a proxy) is ignored.
 */
function problemDetail(body: unknown): string | undefined {
  if (!body || typeof body !== 'object') {
    return undefined;
  }

  const problem = body as { detail?: unknown; errors?: Record<string, unknown> };
  if (typeof problem.detail === 'string' && problem.detail) {
    return problem.detail;
  }

  const firstFieldErrors = problem.errors ? Object.values(problem.errors)[0] : undefined;
  return Array.isArray(firstFieldErrors) && typeof firstFieldErrors[0] === 'string' ? firstFieldErrors[0] : undefined;
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
      return 'אנא בדוק את הערכים שהוזנו.';
    case 'not-found':
      return 'הבקשה לא נמצאה.';
    case 'conflict':
      return 'הבקשה עודכנה על ידי משתמש אחר. אנא רענן את הבקשה לפני ביצוע שינוי נוסף.';
    case 'server':
      return 'משהו השתבש בשרת. אנא נסה שוב.';
    default:
      return 'אירעה שגיאה בלתי צפויה.';
  }
}
