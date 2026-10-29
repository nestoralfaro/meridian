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
    path: 'accounts/:accountId',
    title: 'Account | Meridian',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/account/account-page').then((m) => m.AccountPage),
  },
  {
    path: 'transfer',
    title: 'Move money | Meridian',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/transfer/transfer-page').then((m) => m.TransferPage),
  },
  {
    path: 'planner',
    title: 'Retirement planner | Meridian',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/planner/planner-page').then((m) => m.PlannerPage),
  },
  {
    path: 'learn',
    title: 'Learn | Meridian',
    loadComponent: () => import('./pages/learn/learn-page').then((m) => m.LearnPage),
  },
  {
    path: 'learn/:slug',
    title: 'Learn | Meridian',
    loadComponent: () => import('./pages/learn/article-page').then((m) => m.ArticlePage),
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
