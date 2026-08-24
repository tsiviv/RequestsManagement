export type ApiErrorKind = 'validation' | 'not-found' | 'conflict' | 'server' | 'network' | 'unknown';

export interface ApiError {
  readonly kind: ApiErrorKind;
  readonly message: string;
  readonly status: number | null;
}
