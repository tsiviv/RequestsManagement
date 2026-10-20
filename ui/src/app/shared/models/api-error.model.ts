export type ApiErrorKind = 'validation' | 'not-found' | 'conflict' | 'server' | 'network' | 'unknown';

export interface ApiError {
  readonly kind: ApiErrorKind;
  /** User-facing Hebrew message for the error kind. */
  readonly message: string;
  readonly status: number | null;
  /**
   * The server's own explanation from the ProblemDetails body (`detail`, or the first field error),
   * e.g. why a transition was rejected. In Hebrew, like every message the API returns.
   */
  readonly detail?: string;
}
