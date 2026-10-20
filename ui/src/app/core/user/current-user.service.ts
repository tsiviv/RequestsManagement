import { Injectable, computed, signal } from '@angular/core';

const STORAGE_KEY = 'requests-management.userName';

/** Mirrors the server's ChangedBy column limit (RequestStatusHistoryEntity.ChangedByMaxLength). */
export const USER_NAME_MAX_LENGTH = 100;

/**
 * The name recorded as ChangedBy for status changes. There is no authentication, so the user
 * types it once (toolbar field) and it is remembered in localStorage.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly _userName = signal(readStoredName());

  readonly userName = this._userName.asReadonly();
  readonly hasUserName = computed(() => this._userName().length > 0);

  setUserName(value: string): void {
    const name = value.trim().slice(0, USER_NAME_MAX_LENGTH);
    this._userName.set(name);
    try {
      if (name) {
        localStorage.setItem(STORAGE_KEY, name);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // Storage can be unavailable (private mode, blocked site data); the name still works for this session.
    }
  }
}

function readStoredName(): string {
  try {
    return localStorage.getItem(STORAGE_KEY)?.trim() ?? '';
  } catch {
    return '';
  }
}
