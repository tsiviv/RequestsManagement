import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'requests',
  },
  {
    path: 'requests',
    loadComponent: () =>
      import('./features/requests/requests-page.component').then((m) => m.RequestsPageComponent),
    title: 'Requests · Request Management',
  },
  {
    path: '**',
    redirectTo: 'requests',
  },
];
