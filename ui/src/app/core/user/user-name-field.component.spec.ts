import { TestBed } from '@angular/core/testing';
import { UserNameFieldComponent } from './user-name-field.component';
import { CurrentUserService } from './current-user.service';

describe('UserNameFieldComponent', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function setup() {
    const fixture = TestBed.createComponent(UserNameFieldComponent);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input')!;
    return { fixture, input, currentUser: TestBed.inject(CurrentUserService) };
  }

  it('shows the remembered name', () => {
    localStorage.setItem('requests-management.userName', 'דנה כהן');
    const { input } = setup();
    expect(input.value).toBe('דנה כהן');
  });

  it('stores what the user typed once the field is committed', () => {
    const { input, currentUser } = setup();

    input.value = '  יוסי לוי ';
    input.dispatchEvent(new Event('change'));

    expect(currentUser.userName()).toBe('יוסי לוי');
    expect(localStorage.getItem('requests-management.userName')).toBe('יוסי לוי');
  });

  it('limits input to the server-side length', () => {
    const { input } = setup();
    expect(input.maxLength).toBe(100);
  });
});
