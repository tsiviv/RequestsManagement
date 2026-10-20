import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CurrentUserService, USER_NAME_MAX_LENGTH } from './current-user.service';

const STORAGE_KEY = 'requests-management.userName';

describe('CurrentUserService', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function create(): CurrentUserService {
    TestBed.resetTestingModule();
    return TestBed.inject(CurrentUserService);
  }

  it('starts empty when nothing is stored', () => {
    const service = create();
    expect(service.userName()).toBe('');
    expect(service.hasUserName()).toBe(false);
  });

  it('restores the name remembered from a previous visit', () => {
    localStorage.setItem(STORAGE_KEY, 'דנה כהן');
    expect(create().userName()).toBe('דנה כהן');
  });

  it('trims, caps at the server limit, and persists the name', () => {
    const service = create();

    service.setUserName(`  ${'א'.repeat(USER_NAME_MAX_LENGTH + 5)}  `);

    expect(service.userName().length).toBe(USER_NAME_MAX_LENGTH);
    expect(localStorage.getItem(STORAGE_KEY)).toBe(service.userName());
    expect(service.hasUserName()).toBe(true);
  });

  it('clearing the name forgets it', () => {
    const service = create();
    service.setUserName('דנה');

    service.setUserName('   ');

    expect(service.hasUserName()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('keeps working for the session when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    const service = create();
    service.setUserName('דנה');

    expect(service.userName()).toBe('דנה');
  });
});
