import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../core/auth.service';
import { describeHttpError } from '../core/http-error';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="login-wrap">
      <div class="card shadow-sm login-card">
        <div class="card-body p-4">
          <div class="d-flex align-items-center gap-2 mb-3"><span class="brand-mark brand-mark-dark">S</span><span class="fw-semibold">Satbayev University</span></div>
          <h1 class="h4 mb-1">Электронный силлабус + УМКД</h1>
          <p class="text-muted small mb-4">Войдите с учётной записью университета</p>
          <form (ngSubmit)="submit()" #f="ngForm" novalidate>
            @if (error()) { <div class="alert alert-danger py-2 small" role="alert" data-test="login-error">{{ error() }}</div> }
            <div class="mb-3">
              <label class="form-label" for="email">E-mail</label>
              <input class="form-control" id="email" name="email" type="email" [(ngModel)]="email" required email autocomplete="username" #e="ngModel" />
              @if (e.invalid && (e.touched || f.submitted)) { <div class="text-danger small">Введите корректный e-mail</div> }
            </div>
            <div class="mb-3">
              <label class="form-label" for="password">Пароль</label>
              <input class="form-control" id="password" name="password" type="password" [(ngModel)]="password" required autocomplete="current-password" #p="ngModel" />
              @if (p.invalid && (p.touched || f.submitted)) { <div class="text-danger small">Введите пароль</div> }
            </div>
            <button class="btn btn-primary w-100" type="submit" [disabled]="busy()">{{ busy() ? 'Вход…' : 'Войти' }}</button>
          </form>
        </div>
      </div>
      <div class="card mt-3 login-card small">
        <div class="card-body">
          <div class="fw-semibold mb-2">Демо-учётные записи (пароль <code>Demo123!</code>)</div>
          @for (d of demo; track d[0]) {
            <div class="d-flex justify-content-between"><a href="" (click)="$event.preventDefault(); email = d[0]; password = 'Demo123!'">{{ d[0] }}</a><span class="text-muted">{{ d[1] }}</span></div>
          }
        </div>
      </div>
    </div>`
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  email = '';
  password = '';
  busy = signal(false);
  error = signal<string | null>(null);
  demo = [
    ['teacher@esyllabus.local', 'Преподаватель'], ['head@esyllabus.local', 'Заведующий кафедрой'],
    ['director@esyllabus.local', 'Директор института'], ['student@esyllabus.local', 'Обучающийся'], ['admin@esyllabus.local', 'Администратор']
  ];

  async submit() {
    if (!this.email || !this.password) { this.error.set('Введите e-mail и пароль.'); return; }
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.email.trim(), this.password);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/');
    } catch (err) {
      this.error.set(err instanceof HttpErrorResponse && err.status === 401
        ? (err.error?.title ?? 'Неверный e-mail или пароль.') : describeHttpError(err).message);
    } finally {
      this.busy.set(false);
    }
  }
}
