import { Routes } from '@angular/router';

export const AUTH_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  { path: 'login', loadComponent: () => import('./login/login-page').then((m) => m.LoginPage) },
  {
    path: 'register',
    loadComponent: () => import('./register/register-page').then((m) => m.RegisterPage),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./forgot-password/forgot-password-page').then((m) => m.ForgotPasswordPage),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./reset-password/reset-password-page').then((m) => m.ResetPasswordPage),
  },
  {
    path: 'confirm-email',
    loadComponent: () =>
      import('./confirm-email/confirm-email-page').then((m) => m.ConfirmEmailPage),
  },
  {
    path: 'oauth-callback',
    loadComponent: () =>
      import('./oauth-callback/oauth-callback-page').then((m) => m.OAuthCallbackPage),
  },
];
