import { HttpErrorResponse } from '@angular/common/http';

/** Разбор ProblemDetails / ValidationProblemDetails из ответа API в понятный текст. */
export function describeHttpError(err: unknown): { message: string; details: string[]; traceId?: string } {
  if (!(err instanceof HttpErrorResponse)) {
    return { message: err instanceof Error ? err.message : 'Неизвестная ошибка', details: [] };
  }
  const body = err.error && typeof err.error === 'object' ? err.error : null;
  const details: string[] = [];
  if (body?.errors && typeof body.errors === 'object') {
    for (const list of Object.values(body.errors as Record<string, string[]>)) details.push(...list);
  }
  if (Array.isArray(body?.issues)) {
    for (const i of body.issues) details.push(`${i.section}: ${i.message}`);
  }
  const traceId: string | undefined = body?.traceId;
  let message: string;
  switch (err.status) {
    case 0: message = 'Сервер недоступен. Проверьте подключение к сети.'; break;
    case 401: message = body?.title ?? 'Требуется вход в систему.'; break;
    case 403: message = 'Недостаточно прав для этого действия.'; break;
    case 404: message = 'Запрашиваемые данные не найдены.'; break;
    case 409: message = body?.title ?? 'Данные были изменены другим пользователем. Обновите страницу.'; break;
    case 413: message = 'Файл слишком большой.'; break;
    case 429: message = 'Слишком много запросов. Повторите попытку позже.'; break;
    default:
      message = err.status >= 500
        ? 'Внутренняя ошибка сервера. Сообщите администратору код обращения.'
        : body?.title ?? (typeof err.error === 'string' && err.error ? err.error : 'Не удалось выполнить запрос.');
  }
  if (err.status >= 500 && traceId) details.push(`Код обращения: ${traceId}`);
  return { message, details, traceId };
}
