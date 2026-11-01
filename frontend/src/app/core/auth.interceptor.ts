import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { NotifyService } from './notify.service';
import { LoggerService } from './logger.service';
import { describeHttpError } from './http-error';

/**
 * Добавляет JWT к запросам API и централизованно обрабатывает ошибки:
 * 401 → выход и переход на вход; 0/403/404/409/413/5xx → уведомление.
 * Ошибки 400 (валидация) по умолчанию тоже показываются; чтобы обработать их на месте,
 * передайте заголовок X-Skip-Error-Toast.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const notify = inject(NotifyService);
  const log = inject(LoggerService);
  const router = inject(Router);

  const skipToast = req.headers.has('X-Skip-Error-Toast');
  let request = req.headers.has('X-Skip-Error-Toast') ? req.clone({ headers: req.headers.delete('X-Skip-Error-Toast') }) : req;
  const token = auth.token;
  if (token && request.url.startsWith('/api/')) {
    request = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(request).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        const info = describeHttpError(err);
        const level = err.status >= 500 || err.status === 0 ? 'error' : 'warn';
        log[level](`HTTP ${err.status} ${request.method} ${request.url}`, info);
        if (err.status === 401 && !request.url.endsWith('/api/auth/login')) {
          notify.warning('Сеанс истёк. Войдите снова.');
          auth.logout(router.url);
        } else if (!skipToast && !request.url.endsWith('/api/auth/login')) {
          notify.error(info.message, info.details);
        }
      }
      return throwError(() => err);
    })
  );
};
