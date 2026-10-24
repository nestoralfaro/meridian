import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Meridian Client Portal',
    loadComponent: () => import('./pages/landing/landing-page').then((m) => m.LandingPage),
  },
  {
    path: 'dashboard',
    title: 'Dashboard | Meridian',
    loadComponent: () => import('./pages/dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: '**',
    title: 'Page not found | Meridian',
    loadComponent: () => import('./pages/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
