import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';
import { LoginComponent } from './pages/login.component';
import { ForbiddenComponent, NotFoundComponent } from './pages/misc.component';

const STAFF = ['Admin', 'Director', 'HeadOfDepartment', 'Teacher'];

export const routes: Routes = [
  { path: 'login', component: LoginComponent, title: 'Вход — Электронный силлабус' },
  {
    path: 'syllabi/:id/print', canActivate: [authGuard], title: 'Печать силлабуса',
    loadComponent: () => import('./pages/syllabus-print.component').then(m => m.SyllabusPrintComponent)
  },
  {
    path: '', component: ShellComponent, canActivate: [authGuard],
    children: [
      { path: '', title: 'Главная — Электронный силлабус', loadComponent: () => import('./pages/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'syllabi', title: 'Силлабусы', loadComponent: () => import('./pages/syllabi-list.component').then(m => m.SyllabiListComponent) },
      {
        path: 'syllabi/new', title: 'Новый силлабус', canActivate: [roleGuard(...STAFF)],
        canDeactivate: [(c: { canDeactivate: () => boolean }) => c.canDeactivate()],
        loadComponent: () => import('./pages/syllabus-edit.component').then(m => m.SyllabusEditComponent)
      },
      { path: 'syllabi/:id', title: 'Силлабус', loadComponent: () => import('./pages/syllabus-details.component').then(m => m.SyllabusDetailsComponent) },
      {
        path: 'syllabi/:id/edit', title: 'Редактирование силлабуса', canActivate: [roleGuard(...STAFF)],
        canDeactivate: [(c: { canDeactivate: () => boolean }) => c.canDeactivate()],
        loadComponent: () => import('./pages/syllabus-edit.component').then(m => m.SyllabusEditComponent)
      },
      { path: 'umkd', title: 'Комплектность УМКД', canActivate: [roleGuard(...STAFF)], loadComponent: () => import('./pages/umkd-overview.component').then(m => m.UmkdOverviewComponent) },
      { path: 'admin', title: 'Администрирование', canActivate: [roleGuard('Admin')], loadComponent: () => import('./pages/admin.component').then(m => m.AdminComponent) },
      { path: 'forbidden', component: ForbiddenComponent, title: 'Нет доступа' },
      { path: '**', component: NotFoundComponent, title: 'Страница не найдена' }
    ]
  }
];
