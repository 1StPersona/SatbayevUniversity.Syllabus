import { Injectable, signal } from '@angular/core';

export interface Toast { id: number; kind: 'success' | 'error' | 'warning' | 'info'; text: string; details?: string[]; }

/** Всплывающие уведомления (успех/ошибка) для всего приложения. */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private seq = 0;
  readonly toasts = signal<Toast[]>([]);

  success(text: string) { this.push({ kind: 'success', text }, 4000); }
  info(text: string) { this.push({ kind: 'info', text }, 4000); }
  warning(text: string, details?: string[]) { this.push({ kind: 'warning', text, details }, 8000); }
  error(text: string, details?: string[]) { this.push({ kind: 'error', text, details }, 10000); }

  dismiss(id: number) { this.toasts.update(list => list.filter(t => t.id !== id)); }

  private push(t: Omit<Toast, 'id'>, ttl: number) {
    const id = ++this.seq;
    // Не дублируем одинаковые сообщения, пришедшие подряд (например, несколько упавших запросов).
    if (this.toasts().some(x => x.text === t.text && x.kind === t.kind)) return;
    this.toasts.update(list => [...list, { ...t, id }].slice(-5));
    setTimeout(() => this.dismiss(id), ttl);
  }
}
