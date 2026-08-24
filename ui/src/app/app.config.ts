import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeHe from '@angular/common/locales/he';
import { MatPaginatorIntl } from '@angular/material/paginator';

import { routes } from './app.routes';
import { httpErrorInterceptor } from './core/interceptors/http-error.interceptor';
import { HebrewPaginatorIntl } from './core/i18n/hebrew-paginator-intl';

registerLocaleData(localeHe);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([httpErrorInterceptor])),
    { provide: LOCALE_ID, useValue: 'he' },
    { provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl },
  ],
};
