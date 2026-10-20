import { Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { CurrentUserService, USER_NAME_MAX_LENGTH } from './current-user.service';

/** Toolbar field for the name recorded on status changes (see CurrentUserService). */
@Component({
  selector: 'app-user-name-field',
  imports: [MatFormFieldModule, MatInputModule, MatIconModule],
  template: `
    <mat-form-field class="user-name-field" appearance="outline" subscriptSizing="dynamic">
      <mat-icon matPrefix aria-hidden="true">person</mat-icon>
      <input
        #nameInput
        matInput
        aria-label="שם המשתמש שיירשם בהיסטוריית השינויים"
        placeholder="השם שלך"
        [maxLength]="maxLength"
        [value]="currentUser.userName()"
        (change)="currentUser.setUserName(nameInput.value)"
      />
    </mat-form-field>
  `,
  styles: `
    .user-name-field {
      width: 14rem;
      max-width: 45vw;
      font-size: 0.875rem;
      --mat-form-field-container-height: 40px;
      --mat-form-field-container-vertical-padding: 8px;
    }
  `,
})
export class UserNameFieldComponent {
  protected readonly currentUser = inject(CurrentUserService);
  protected readonly maxLength = USER_NAME_MAX_LENGTH;
}
