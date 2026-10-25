import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    title: 'Meridian Client Portal',
    loadComponent: () => import('./pages/landing/landing-page').then((m) => m.LandingPage),
  },
  {
    path: 'sign-in',
    title: 'Sign in | Meridian',
    loadComponent: () => import('./pages/sign-in/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'register',
    title: 'Create your account | Meridian',
    loadComponent: () => import('./pages/register/register-page').then((m) => m.RegisterPage),
  },
  {
    path: 'dashboard',
    title: 'Dashboard | Meridian',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'activity',
    title: 'Activity | Meridian',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/activity/activity-page').then((m) => m.ActivityPage),
  },
  {
    path: '**',
    title: 'Page not found | Meridian',
    loadComponent: () => import('./pages/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
