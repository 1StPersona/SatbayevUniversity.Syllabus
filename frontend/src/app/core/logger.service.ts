import { Injectable, isDevMode } from '@angular/core';

/** Клиентский журнал: в разработке — консоль, в эксплуатации — только предупреждения и ошибки. */
@Injectable({ providedIn: 'root' })
export class LoggerService {
  debug(message: string, ...data: unknown[]) { if (isDevMode()) console.debug(`[syllabus] ${message}`, ...data); }
  info(message: string, ...data: unknown[]) { if (isDevMode()) console.info(`[syllabus] ${message}`, ...data); }
  warn(message: string, ...data: unknown[]) { console.warn(`[syllabus] ${message}`, ...data); }
  error(message: string, ...data: unknown[]) { console.error(`[syllabus] ${message}`, ...data); }
}
