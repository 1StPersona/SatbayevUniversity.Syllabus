import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ROLE_TITLES } from '../core/models';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <nav class="navbar navbar-expand-lg navbar-dark app-navbar no-print">
      <div class="container-fluid">
        <a class="navbar-brand d-flex align-items-center gap-2" routerLink="/">
          <span class="brand-mark">S</span><span>Электронный силлабус <span class="opacity-75">+ УМКД</span></span>
        </a>
        <button class="navbar-toggler" type="button" (click)="open = !open" aria-label="Меню"><span class="navbar-toggler-icon"></span></button>
        <div class="collapse navbar-collapse" [class.show]="open">
          <ul class="navbar-nav me-auto">
            <li class="nav-item"><a class="nav-link" routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}">Главная</a></li>
            <li class="nav-item"><a class="nav-link" routerLink="/syllabi" routerLinkActive="active">{{ auth.isStaff() ? 'Силлабусы' : 'Мои дисциплины' }}</a></li>
            @if (auth.isStaff()) { <li class="nav-item"><a class="nav-link" routerLink="/umkd" routerLinkActive="active">УМКД</a></li> }
            @if (auth.isAdmin()) { <li class="nav-item"><a class="nav-link" routerLink="/admin" routerLinkActive="active">Администрирование</a></li> }
          </ul>
          <div class="d-flex align-items-center gap-3 text-white">
            <div class="text-end lh-sm small">
              <div>{{ auth.user()?.fullName }}</div>
              <div class="opacity-75">{{ roles() }}</div>
            </div>
            <button class="btn btn-sm btn-outline-light" (click)="auth.logout()">Выйти</button>
          </div>
        </div>
      </div>
    </nav>
    <main class="container-xl py-4"><router-outlet /></main>
    <footer class="app-footer text-muted small no-print"><div class="container-xl py-3">SatbayevUniversity.Syllabus · Электронный силлабус + УМКД</div></footer>
  `
})
export class ShellComponent {
  auth = inject(AuthService);
  open = false;
  roles = computed(() => (this.auth.user()?.roles ?? []).map(r => ROLE_TITLES[r] ?? r).join(', '));
}
