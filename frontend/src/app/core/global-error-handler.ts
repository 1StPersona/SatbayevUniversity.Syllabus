import { ErrorHandler, inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { NotifyService } from './notify.service';
import { LoggerService } from './logger.service';

/** Перехват необработанных ошибок (исключения в компонентах, отклонённые промисы). */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private notify = inject(NotifyService);
  private log = inject(LoggerService);

  handleError(error: unknown): void {
    const err = (error as { rejection?: unknown })?.rejection ?? error;
    // HTTP-ошибки уже показаны перехватчиком.
    if (err instanceof HttpErrorResponse) return;
    this.log.error('Необработанная ошибка', err);
    this.notify.error('Произошла непредвиденная ошибка интерфейса. Обновите страницу.');
  }
}
